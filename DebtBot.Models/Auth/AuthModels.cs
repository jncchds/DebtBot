namespace DebtBot.Models.Auth;

public class LoginModel
{
    public string? Login { get; set; }
    public string? Password { get; set; }
}

/// <summary>
/// Sets or changes the current user's login and password.
/// The first time, <see cref="Login"/> and <see cref="NewPassword"/> are required.
/// After that, any change needs <see cref="CurrentPassword"/>; leave <see cref="NewPassword"/> empty to change only the login.
/// </summary>
public class SetPasswordModel
{
    public string? Login { get; set; }
    public string? CurrentPassword { get; set; }
    public string? NewPassword { get; set; }
}

/// <summary>
/// An admin sets a user's login and password. <see cref="Login"/> may be left empty when the user already has one.
/// </summary>
public class AdminSetPasswordModel
{
    public string? Login { get; set; }
    public string? Password { get; set; }
}

public class SessionModel
{
    public SessionUserModel User { get; set; } = new();
    public string AccessToken { get; set; } = "";
    public DateTime AccessTokenExpiresAt { get; set; }
}

public class SessionUserModel
{
    public Guid Id { get; set; }
    public string DisplayName { get; set; } = "";
    public string? TelegramUserName { get; set; }
    public bool IsAdmin { get; set; }
}

public class AuthConfigModel
{
    /// <summary>The bot's username for the Telegram Login Widget, or null when the bot isn't configured.</summary>
    public string? TelegramBotUsername { get; set; }
}

public enum PasswordChangeResult
{
    Success,
    UserNotFound,
    LoginRequired,
    InvalidLogin,
    LoginTaken,
    PasswordRequired,
    PasswordTooShort,
    CurrentPasswordRequired,
    CurrentPasswordIncorrect,
}
