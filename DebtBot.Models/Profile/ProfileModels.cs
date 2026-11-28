using DebtBot.Models.Enums;

namespace DebtBot.Models.Profile;

/// <summary>The signed-in user's own view of their account.</summary>
public class ProfileModel
{
    public Guid Id { get; set; }
    public string DisplayName { get; set; } = "";
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public long? TelegramId { get; set; }
    public string? TelegramUserName { get; set; }
    /// <summary>True once the user has started the bot in Telegram.</summary>
    public bool TelegramBotEnabled { get; set; }
    public UserRole Role { get; set; }
    /// <summary>Web sign-in login, or null.</summary>
    public string? Login { get; set; }
    public bool HasPassword { get; set; }
    /// <summary>Where a read receipt opens on the web: Form (default) or Text.</summary>
    public ReceiptOpenIn ReceiptOpenIn { get; set; }
}

/// <summary>
/// Replaces the editable profile fields. Telegram fields, login and role can't be changed here.
/// Empty or missing phone or email clears it.
/// </summary>
public class ProfileUpdateModel
{
    public string DisplayName { get; set; } = "";
    public string? Phone { get; set; }
    public string? Email { get; set; }
    /// <summary>Form or Text; left out (null) keeps the current setting.</summary>
    public ReceiptOpenIn? ReceiptOpenIn { get; set; }
}
