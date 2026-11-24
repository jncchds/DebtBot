using DebtBot.Models.Auth;
using DebtBot.Models.User;

namespace DebtBot.Interfaces.Services;

public interface IAuthService
{
    /// <summary>Verifies a Telegram Login Widget payload, then finds or creates the user the same way the bot does.</summary>
    Task<AuthSignInResult> SignInWithTelegramAsync(TelegramLoginModel model, CancellationToken cancellationToken = default);

    /// <summary>Checks a login and password. Failures don't say whether the login or the password was wrong.</summary>
    Task<AuthSignInResult> SignInWithPasswordAsync(string? login, string? password, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets or changes the user's own login and password. A password change signs out the user's other web sessions;
    /// <c>currentRefreshToken</c> (the caller's own) is kept.
    /// </summary>
    Task<PasswordChangeResult> SetOwnPasswordAsync(Guid userId, SetPasswordModel model, string? currentRefreshToken = null, CancellationToken cancellationToken = default);

    /// <summary>An admin sets any user's login and password. Signs that user out of all web sessions.</summary>
    Task<PasswordChangeResult> AdminSetPasswordAsync(Guid userId, AdminSetPasswordModel model, CancellationToken cancellationToken = default);

    SessionModel CreateSession(UserModel user);

    /// <summary>Starts a web session and returns the raw refresh token (only its hash is stored).</summary>
    Task<IssuedRefreshToken> IssueRefreshTokenAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Rotates a refresh token. Returns null when the token is unknown, expired or revoked.
    /// Presenting a token that was already rotated revokes the whole chain that descends from it.
    /// </summary>
    Task<RefreshResult?> RefreshAsync(string rawToken, CancellationToken cancellationToken = default);

    /// <summary>Revokes one refresh token (sign-out). Unknown tokens are ignored.</summary>
    Task RevokeRefreshTokenAsync(string rawToken, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes refresh tokens that can never be used again: expired ones, and ones revoked more than
    /// <c>AuthService.RevokedTokenRetention</c> ago (kept that long so a replayed old token is still
    /// recognised and revokes its chain). Returns how many rows were deleted.
    /// </summary>
    Task<int> PurgeRefreshTokensAsync(CancellationToken cancellationToken = default);
}

public record AuthSignInResult(UserModel? User, string? Error = null, bool LockedOut = false)
{
    public bool Succeeded => User is not null;
}

public record IssuedRefreshToken(string Token, DateTime ExpiresAt);

public record RefreshResult(UserModel User, IssuedRefreshToken RefreshToken);
