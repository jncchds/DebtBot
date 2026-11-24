namespace DebtBot.Models.Contact;

public enum SubscriptionState
{
    None = 0,
    /// <summary>Requested, waiting for the other side to confirm.</summary>
    Pending = 1,
    Confirmed = 2
}

/// <summary>One of the signed-in user's contacts.</summary>
public class ContactModel
{
    /// <summary>The contact's user id.</summary>
    public Guid Id { get; set; }
    /// <summary>The name the signed-in user gave this contact.</summary>
    public string DisplayName { get; set; } = "";
    /// <summary>The contact's own display name.</summary>
    public string UserDisplayName { get; set; } = "";
    public long? TelegramId { get; set; }
    public string? TelegramUserName { get; set; }
    /// <summary>False for placeholder users who never started the bot.</summary>
    public bool TelegramBotEnabled { get; set; }
    /// <summary>Whether I get notified about this contact's bills (I'm the subscriber).</summary>
    public SubscriptionState MySubscription { get; set; }
    /// <summary>Whether this contact gets notified about my bills (they're the subscriber); Pending means I can confirm or decline.</summary>
    public SubscriptionState TheirSubscription { get; set; }
    /// <summary>When the contact was added (UTC); null for contacts added before this was recorded.</summary>
    public DateTime? AddedAt { get; set; }
}

public class ContactRenameModel
{
    public string DisplayName { get; set; } = "";
}
