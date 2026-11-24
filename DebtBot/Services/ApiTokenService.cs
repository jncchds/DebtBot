using DebtBot.DB;
using DebtBot.DB.Entities;
using DebtBot.Models.Enums;
using DebtBot.Models.Profile;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.Security.Cryptography;

namespace DebtBot.Services;

public enum ApiTokenCreateStatus
{
    Created,
    Invalid,
    LimitReached,
}

public record ApiTokenCreateResult(ApiTokenCreateStatus Status, ApiTokenCreatedModel? Token = null, string? Error = null);

/// <summary>Who an API token belongs to, read from the database when the token is used.</summary>
public record ApiTokenIdentity(Guid TokenId, ApiTokenScope Scope, Guid UserId, string DisplayName, UserRole Role);

public interface IApiTokenService
{
    /// <summary>The user's tokens: active first, then expired, then revoked; newest first within each.</summary>
    List<ApiTokenModel> List(Guid userId);

    /// <summary>Makes a token. The secret is in the result and nowhere else.</summary>
    Task<ApiTokenCreateResult> CreateAsync(Guid userId, ApiTokenCreationModel model, CancellationToken cancellationToken = default);

    /// <summary>Revokes one of the user's tokens. False when the user has no such token. Revoking twice is fine.</summary>
    Task<bool> RevokeAsync(Guid userId, Guid tokenId, CancellationToken cancellationToken = default);

    /// <summary>Revokes all the user's active tokens. Returns how many.</summary>
    Task<int> RevokeAllAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks a raw <c>dbt_…</c> token: null when it's unknown, revoked or expired, or its user is gone.
    /// Writes <c>LastUsedAt</c> at most once a minute per token.
    /// </summary>
    Task<ApiTokenIdentity?> AuthenticateAsync(string rawToken, CancellationToken cancellationToken = default);
}

public class ApiTokenService : IApiTokenService
{
    public const string TokenPrefix = "dbt_";
    public const int PrefixLength = 8;
    public const int MaxNameLength = 100;
    public const int SecretBytes = 32;
    public static readonly int[] AllowedExpiryDays = [30, 90, 365];
    public static readonly TimeSpan LastUsedPrecision = TimeSpan.FromMinutes(1);

    private readonly DebtContext _db;
    private readonly TimeProvider _timeProvider;
    private readonly ApiTokenConfiguration _config;

    public ApiTokenService(DebtContext db, TimeProvider timeProvider, IOptions<DebtBotConfiguration> config)
    {
        _db = db;
        _timeProvider = timeProvider;
        _config = config.Value.ApiTokens ?? new ApiTokenConfiguration();
    }

    private DateTime UtcNow => _timeProvider.GetUtcNow().UtcDateTime;

    public static bool LooksLikeApiToken(string? value) =>
        value is not null && value.StartsWith(TokenPrefix, StringComparison.Ordinal);

    public static ApiTokenStatus StatusOf(ApiToken token, DateTime now) =>
        token.RevokedAt is not null ? ApiTokenStatus.Revoked
        : token.ExpiresAt is DateTime expires && expires <= now ? ApiTokenStatus.Expired
        : ApiTokenStatus.Active;

    private static ApiTokenModel ToModel(ApiToken t, DateTime now) => new()
    {
        Id = t.Id,
        Name = t.Name,
        Prefix = t.Prefix,
        Scope = t.Scope,
        CreatedAt = t.CreatedAt,
        LastUsedAt = t.LastUsedAt,
        ExpiresAt = t.ExpiresAt,
        RevokedAt = t.RevokedAt,
        Status = StatusOf(t, now),
    };

    public List<ApiTokenModel> List(Guid userId)
    {
        var now = UtcNow;
        return _db.ApiTokens
            .AsNoTracking()
            .Where(t => t.UserId == userId)
            .ToList()
            .Select(t => ToModel(t, now))
            .OrderBy(t => t.Status)
            .ThenByDescending(t => t.CreatedAt)
            .ToList();
    }

    public async Task<ApiTokenCreateResult> CreateAsync(Guid userId, ApiTokenCreationModel model, CancellationToken cancellationToken = default)
    {
        var name = (model.Name ?? "").Trim();
        if (name.Length is 0 or > MaxNameLength)
        {
            return new(ApiTokenCreateStatus.Invalid, Error: $"Enter a name (1–{MaxNameLength} characters).");
        }
        if (!Enum.IsDefined(model.Scope))
        {
            return new(ApiTokenCreateStatus.Invalid, Error: "The scope is Read or ReadWrite.");
        }
        if (model.ExpiresInDays is int days && !AllowedExpiryDays.Contains(days))
        {
            return new(ApiTokenCreateStatus.Invalid, Error: "expiresInDays is 30, 90, 365 or null (never).");
        }

        var now = UtcNow;
        var active = await _db.ApiTokens.CountAsync(t => t.UserId == userId
            && t.RevokedAt == null
            && (t.ExpiresAt == null || t.ExpiresAt > now), cancellationToken);
        if (active >= _config.MaxActivePerUser)
        {
            return new(ApiTokenCreateStatus.LimitReached,
                Error: $"You already have {_config.MaxActivePerUser} active tokens. Revoke one first.");
        }

        var raw = TokenPrefix + Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(SecretBytes));
        var token = new ApiToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = name,
            Prefix = raw.Substring(TokenPrefix.Length, PrefixLength),
            Hash = AuthService.HashToken(raw),
            Scope = model.Scope,
            CreatedAt = now,
            ExpiresAt = model.ExpiresInDays is int d ? now.AddDays(d) : null,
        };
        _db.ApiTokens.Add(token);
        await _db.SaveChangesAsync(cancellationToken);

        var record = ToModel(token, now);
        return new(ApiTokenCreateStatus.Created, new ApiTokenCreatedModel
        {
            Id = record.Id,
            Name = record.Name,
            Prefix = record.Prefix,
            Scope = record.Scope,
            CreatedAt = record.CreatedAt,
            LastUsedAt = record.LastUsedAt,
            ExpiresAt = record.ExpiresAt,
            RevokedAt = record.RevokedAt,
            Status = record.Status,
            Token = raw,
        });
    }

    public async Task<bool> RevokeAsync(Guid userId, Guid tokenId, CancellationToken cancellationToken = default)
    {
        var token = await _db.ApiTokens.FirstOrDefaultAsync(t => t.Id == tokenId && t.UserId == userId, cancellationToken);
        if (token is null)
        {
            return false;
        }

        if (token.RevokedAt is null)
        {
            token.RevokedAt = UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
        }

        return true;
    }

    public async Task<int> RevokeAllAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var now = UtcNow;
        var tokens = await _db.ApiTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null && (t.ExpiresAt == null || t.ExpiresAt > now))
            .ToListAsync(cancellationToken);
        foreach (var token in tokens)
        {
            token.RevokedAt = now;
        }
        await _db.SaveChangesAsync(cancellationToken);
        return tokens.Count;
    }

    public async Task<ApiTokenIdentity?> AuthenticateAsync(string rawToken, CancellationToken cancellationToken = default)
    {
        if (!LooksLikeApiToken(rawToken))
        {
            return null;
        }

        var hash = AuthService.HashToken(rawToken);
        var now = UtcNow;
        var found = await _db.ApiTokens
            .AsNoTracking()
            .Where(t => t.Hash == hash)
            .Select(t => new
            {
                t.Id,
                t.Scope,
                t.ExpiresAt,
                t.RevokedAt,
                t.LastUsedAt,
                t.UserId,
                // The user is read now, so a role change or a merge applies at once.
                User = _db.Users.Where(u => u.Id == t.UserId).Select(u => new { u.DisplayName, u.Role }).FirstOrDefault(),
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (found is null
            || found.RevokedAt is not null
            || (found.ExpiresAt is DateTime expires && expires <= now)
            || found.User is null)
        {
            return null;
        }

        if (found.LastUsedAt is null || now - found.LastUsedAt.Value >= LastUsedPrecision)
        {
            // Conditional, so parallel requests write it once.
            var threshold = now - LastUsedPrecision;
            await _db.ApiTokens
                .Where(t => t.Id == found.Id && (t.LastUsedAt == null || t.LastUsedAt <= threshold))
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.LastUsedAt, now), cancellationToken);
        }

        return new ApiTokenIdentity(found.Id, found.Scope, found.UserId, found.User.DisplayName, found.User.Role);
    }
}
