using DebtBot.DB;
using DebtBot.DB.Entities;
using DebtBot.Interfaces;
using DebtBot.Interfaces.Services;
using DebtBot.Models.Auth;
using DebtBot.Models.Enums;
using DebtBot.Models.User;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace DebtBot.Services;

public partial class AuthService : IAuthService
{
    public const int MinPasswordLength = 8;
    public const int MinLoginLength = 3;
    public const int MaxLoginLength = 64;
    public const string InvalidCredentialsMessage = "Invalid login or password.";

    // Verified against when the login doesn't exist, so a wrong login takes as long as a wrong password.
    private static readonly Lazy<string> DummyHash = new(() => new PasswordHasher<User>().HashPassword(new User(), "not-a-real-password"));

    private readonly DebtContext _db;
    private readonly IUserService _userService;
    private readonly IIdentityService _identityService;
    private readonly IPasswordHasher<User> _passwordHasher;
    private readonly LoginThrottle _loginThrottle;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<AuthService> _logger;
    private readonly DebtBotConfiguration _config;

    public AuthService(
        DebtContext db,
        IUserService userService,
        IIdentityService identityService,
        IPasswordHasher<User> passwordHasher,
        LoginThrottle loginThrottle,
        TimeProvider timeProvider,
        IOptions<DebtBotConfiguration> config,
        ILogger<AuthService> logger)
    {
        _db = db;
        _userService = userService;
        _identityService = identityService;
        _passwordHasher = passwordHasher;
        _loginThrottle = loginThrottle;
        _timeProvider = timeProvider;
        _config = config.Value;
        _logger = logger;
    }

    private DateTime UtcNow => _timeProvider.GetUtcNow().UtcDateTime;

    private TimeSpan RefreshTokenLifetime =>
        TimeSpan.FromDays(_config.JwtConfiguration?.RefreshTokenLifetimeDays is int days and > 0 ? days : 30);

    #region Telegram

    public Task<AuthSignInResult> SignInWithTelegramAsync(TelegramLoginModel model, CancellationToken cancellationToken = default)
    {
        var botToken = _config.Telegram?.BotToken;
        if (!TelegramConfiguration.IsRealBotToken(botToken))
        {
            return Task.FromResult(new AuthSignInResult(null, "Telegram sign-in is not configured."));
        }

        var check = TelegramLoginVerifier.Verify(model, botToken!, _timeProvider.GetUtcNow());
        if (check == TelegramLoginCheck.InvalidHash)
        {
            return Task.FromResult(new AuthSignInResult(null, "Invalid Telegram sign-in data."));
        }
        if (check == TelegramLoginCheck.Expired)
        {
            return Task.FromResult(new AuthSignInResult(null, "Telegram sign-in data has expired, please sign in again."));
        }

        // Same as the bot: update (or create) the user from the Telegram identity, then look it up.
        var firstName = model.FirstName ?? model.Username ?? model.Id.ToString();
        _userService.ActualizeTelegramUser(model.Id, model.Username, firstName, model.LastName);

        var displayName = string.Join(" ", new[] { firstName, model.LastName }.Where(t => !string.IsNullOrWhiteSpace(t)));
        var user = _userService.FindOrAddUser(new UserSearchModel
        {
            TelegramId = model.Id,
            TelegramUserName = model.Username is null ? null : $"@{model.Username}",
            DisplayName = displayName,
        });

        return Task.FromResult(new AuthSignInResult(user));
    }

    #endregion

    #region Passwords

    public async Task<AuthSignInResult> SignInWithPasswordAsync(string? login, string? password, CancellationToken cancellationToken = default)
    {
        var normalizedLogin = NormalizeLogin(login);
        if (normalizedLogin is null || string.IsNullOrEmpty(password))
        {
            return new AuthSignInResult(null, InvalidCredentialsMessage);
        }

        if (_loginThrottle.IsLockedOut(normalizedLogin))
        {
            return new AuthSignInResult(null, "Too many failed sign-in attempts. Try again later.", LockedOut: true);
        }

        var entity = await _db.Users.FirstOrDefaultAsync(u => u.Login == normalizedLogin, cancellationToken);
        if (entity?.PasswordHash is null)
        {
            _passwordHasher.VerifyHashedPassword(new User(), DummyHash.Value, password);
            _loginThrottle.RecordFailure(normalizedLogin);
            return new AuthSignInResult(null, InvalidCredentialsMessage);
        }

        var result = _passwordHasher.VerifyHashedPassword(entity, entity.PasswordHash, password);
        if (result == PasswordVerificationResult.Failed)
        {
            _loginThrottle.RecordFailure(normalizedLogin);
            return new AuthSignInResult(null, InvalidCredentialsMessage);
        }

        if (result == PasswordVerificationResult.SuccessRehashNeeded)
        {
            entity.PasswordHash = _passwordHasher.HashPassword(entity, password);
            await _db.SaveChangesAsync(cancellationToken);
        }

        _loginThrottle.Reset(normalizedLogin);
        return new AuthSignInResult(_userService.GetUserById(entity.Id));
    }

    public async Task<PasswordChangeResult> SetOwnPasswordAsync(Guid userId, SetPasswordModel model, string? currentRefreshToken = null, CancellationToken cancellationToken = default)
    {
        var entity = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (entity is null)
        {
            return PasswordChangeResult.UserNotFound;
        }

        string? newLogin = null;
        if (!string.IsNullOrWhiteSpace(model.Login))
        {
            newLogin = NormalizeLogin(model.Login);
            if (newLogin is null)
            {
                return PasswordChangeResult.InvalidLogin;
            }
        }

        var newPassword = string.IsNullOrEmpty(model.NewPassword) ? null : model.NewPassword;

        if (entity.PasswordHash is null)
        {
            // First time: both a login and a password are needed.
            if (newLogin is null && entity.Login is null)
            {
                return PasswordChangeResult.LoginRequired;
            }
            if (newPassword is null)
            {
                return PasswordChangeResult.PasswordRequired;
            }
        }
        else
        {
            if (newLogin is null && newPassword is null)
            {
                return PasswordChangeResult.PasswordRequired;
            }
            if (string.IsNullOrEmpty(model.CurrentPassword))
            {
                return PasswordChangeResult.CurrentPasswordRequired;
            }
            if (_passwordHasher.VerifyHashedPassword(entity, entity.PasswordHash, model.CurrentPassword) == PasswordVerificationResult.Failed)
            {
                return PasswordChangeResult.CurrentPasswordIncorrect;
            }
        }

        var result = await ApplyLoginAndPasswordAsync(entity, newLogin, newPassword, cancellationToken);
        if (result == PasswordChangeResult.Success && newPassword is not null)
        {
            await RevokeAllForUserAsync(userId, currentRefreshToken, cancellationToken);
        }
        return result;
    }

    public async Task<PasswordChangeResult> AdminSetPasswordAsync(Guid userId, AdminSetPasswordModel model, CancellationToken cancellationToken = default)
    {
        var entity = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (entity is null)
        {
            return PasswordChangeResult.UserNotFound;
        }

        string? newLogin = null;
        if (!string.IsNullOrWhiteSpace(model.Login))
        {
            newLogin = NormalizeLogin(model.Login);
            if (newLogin is null)
            {
                return PasswordChangeResult.InvalidLogin;
            }
        }
        if (newLogin is null && entity.Login is null)
        {
            return PasswordChangeResult.LoginRequired;
        }
        if (string.IsNullOrEmpty(model.Password))
        {
            return PasswordChangeResult.PasswordRequired;
        }

        var result = await ApplyLoginAndPasswordAsync(entity, newLogin, model.Password, cancellationToken);
        if (result == PasswordChangeResult.Success)
        {
            await RevokeAllForUserAsync(userId, null, cancellationToken);
        }
        return result;
    }

    private async Task<PasswordChangeResult> ApplyLoginAndPasswordAsync(User entity, string? newLogin, string? newPassword, CancellationToken cancellationToken)
    {
        if (newPassword is not null && newPassword.Length < MinPasswordLength)
        {
            return PasswordChangeResult.PasswordTooShort;
        }

        if (newLogin is not null && newLogin != entity.Login)
        {
            var taken = await _db.Users.AnyAsync(u => u.Login == newLogin && u.Id != entity.Id, cancellationToken);
            if (taken)
            {
                return PasswordChangeResult.LoginTaken;
            }
            entity.Login = newLogin;
        }

        if (newPassword is not null)
        {
            entity.PasswordHash = _passwordHasher.HashPassword(entity, newPassword);
        }

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (newLogin is not null)
        {
            // Lost a race for the unique login index.
            _logger.LogInformation(ex, "Saving login {Login} failed", newLogin);
            return PasswordChangeResult.LoginTaken;
        }

        return PasswordChangeResult.Success;
    }

    /// <summary>Trims and lowercases a login. Returns null when it isn't a valid login.</summary>
    public static string? NormalizeLogin(string? login)
    {
        if (string.IsNullOrWhiteSpace(login))
        {
            return null;
        }

        var normalized = login.Trim().ToLowerInvariant();
        if (normalized.Length < MinLoginLength || normalized.Length > MaxLoginLength || !LoginRegex().IsMatch(normalized))
        {
            return null;
        }

        return normalized;
    }

    [GeneratedRegex("^[a-z0-9._@-]+$")]
    private static partial Regex LoginRegex();

    #endregion

    #region Sessions and refresh tokens

    public SessionModel CreateSession(UserModel user)
    {
        var accessToken = _identityService.GenerateAccessToken(user);
        return new SessionModel
        {
            User = new SessionUserModel
            {
                Id = user.Id,
                DisplayName = user.DisplayName,
                TelegramUserName = user.TelegramUserName,
                IsAdmin = user.Role == UserRole.Admin,
            },
            AccessToken = accessToken.Token,
            AccessTokenExpiresAt = accessToken.ExpiresAt,
        };
    }

    public async Task<IssuedRefreshToken> IssueRefreshTokenAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var (raw, row) = NewRefreshToken(userId);
        _db.RefreshTokens.Add(row);
        await _db.SaveChangesAsync(cancellationToken);
        return new IssuedRefreshToken(raw, row.ExpiresAt);
    }

    public async Task<RefreshResult?> RefreshAsync(string rawToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(rawToken))
        {
            return null;
        }

        var hash = HashToken(rawToken);
        var stored = await _db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, cancellationToken);
        var now = UtcNow;
        if (stored is null)
        {
            return null;
        }

        if (stored.RevokedAt is not null)
        {
            if (stored.ReplacedById is not null)
            {
                // A rotated-out token came back: someone holds a copy. Revoke everything issued from it.
                _logger.LogWarning("A rotated refresh token was reused for user {UserId}; revoking its chain", stored.UserId);
                await RevokeChainAsync(stored, now, cancellationToken);
            }
            return null;
        }

        if (stored.ExpiresAt <= now)
        {
            return null;
        }

        var user = _userService.GetUserById(stored.UserId);
        if (user is null)
        {
            return null;
        }

        var (newRaw, replacement) = NewRefreshToken(stored.UserId);
        _db.RefreshTokens.Add(replacement);
        stored.RevokedAt = now;
        stored.ReplacedById = replacement.Id;
        await _db.SaveChangesAsync(cancellationToken);

        return new RefreshResult(user, new IssuedRefreshToken(newRaw, replacement.ExpiresAt));
    }

    /// <summary>How long a revoked (rotated or signed-out) refresh token is kept for reuse detection.</summary>
    public static readonly TimeSpan RevokedTokenRetention = TimeSpan.FromDays(7);

    public async Task<int> PurgeRefreshTokensAsync(CancellationToken cancellationToken = default)
    {
        var now = UtcNow;
        var revokedBefore = now - RevokedTokenRetention;
        return await _db.RefreshTokens
            .Where(t => t.ExpiresAt <= now || (t.RevokedAt != null && t.RevokedAt < revokedBefore))
            .ExecuteDeleteAsync(cancellationToken);
    }

    public async Task RevokeRefreshTokenAsync(string rawToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(rawToken))
        {
            return;
        }

        var hash = HashToken(rawToken);
        var stored = await _db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, cancellationToken);
        if (stored is null || stored.RevokedAt is not null)
        {
            return;
        }

        stored.RevokedAt = UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task RevokeChainAsync(RefreshToken start, DateTime now, CancellationToken cancellationToken)
    {
        var visited = new HashSet<Guid> { start.Id };
        var nextId = start.ReplacedById;
        while (nextId is Guid id && visited.Add(id))
        {
            var next = await _db.RefreshTokens.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
            if (next is null)
            {
                break;
            }
            next.RevokedAt ??= now;
            nextId = next.ReplacedById;
        }
        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task RevokeAllForUserAsync(Guid userId, string? exceptRawToken, CancellationToken cancellationToken)
    {
        var exceptHash = string.IsNullOrEmpty(exceptRawToken) ? null : HashToken(exceptRawToken);
        var now = UtcNow;
        var active = await _db.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null && t.TokenHash != exceptHash)
            .ToListAsync(cancellationToken);
        foreach (var token in active)
        {
            token.RevokedAt = now;
        }
        await _db.SaveChangesAsync(cancellationToken);
    }

    private (string Raw, RefreshToken Row) NewRefreshToken(Guid userId)
    {
        var raw = Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(32));
        var now = UtcNow;
        return (raw, new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TokenHash = HashToken(raw),
            CreatedAt = now,
            ExpiresAt = now.Add(RefreshTokenLifetime),
        });
    }

    public static string HashToken(string raw) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw)));

    #endregion
}
