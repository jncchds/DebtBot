using DebtBot.Models.Enums;

namespace DebtBot.Models.User;

/// <summary>A row of the admin users list. Never carries the password hash.</summary>
public class AdminUserModel
{
    public Guid Id { get; set; }
    public string DisplayName { get; set; } = "";
    public long? TelegramId { get; set; }
    public string? TelegramUserName { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public UserRole Role { get; set; }
    public bool TelegramBotEnabled { get; set; }
    public string? Login { get; set; }
    public bool HasPassword { get; set; }
    /// <summary>Bills the user takes part in (creator, payer or line participant).</summary>
    public int BillCount { get; set; }
    /// <summary>Contacts in the user's own contact list.</summary>
    public int ContactCount { get; set; }
    /// <summary>Active (not expired, not revoked) API tokens. Admins never see the tokens themselves.</summary>
    public int ApiTokenCount { get; set; }
    /// <summary>Date of the earliest bill the user takes part in; the "older" user in merge suggestions. Null without bills.</summary>
    public DateTime? FirstBillDate { get; set; }
}

public enum MergeReason
{
    SameTelegramId = 0,
    /// <summary>Same Telegram username, ignoring case and a leading @.</summary>
    SameTelegramUserName = 1
}

/// <summary>A suggested merge of two users that look like the same person.</summary>
public class MergeSuggestionModel
{
    /// <summary>The user to keep (<c>newUserId</c> of <c>POST Users/merge</c>).</summary>
    public AdminUserModel Keep { get; set; } = new();
    /// <summary>The user to merge into <see cref="Keep"/> and remove (<c>oldUserId</c>).</summary>
    public AdminUserModel Merge { get; set; } = new();
    public List<MergeReason> Reasons { get; set; } = [];
}

/// <summary>Merges <see cref="OldUserId"/> into <see cref="NewUserId"/>; the old user is removed.</summary>
public class MergeUsersModel
{
    public Guid OldUserId { get; set; }
    public Guid NewUserId { get; set; }
}
