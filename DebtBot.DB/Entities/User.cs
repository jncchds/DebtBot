using DebtBot.Models.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DebtBot.DB.Entities;

public class User
{
    [Key]
    public Guid Id { get; set; }
    
    public string DisplayName { get; set; }
    public long? TelegramId { get; set; }
    public string? TelegramUserName { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public bool TelegramBotEnabled { get; set; } = false;
    public UserRole Role { get; set; }

    /// <summary>Optional web sign-in login, unique, stored lowercase.</summary>
    [MaxLength(64)]
    public string? Login { get; set; }
    /// <summary>ASP.NET Core Identity password hash; null when the user has no password.</summary>
    public string? PasswordHash { get; set; }

    [InverseProperty(nameof(UserContactLink.User))]
    public virtual ICollection<UserContactLink> UserContacts { get; set; }
//    [InverseProperty(nameof(UserContactLink.ContactUser))]
//    public virtual ICollection<UserContactLink> UsersHavingAsContact { get; set; }
    [InverseProperty(nameof(Debt.CreditorUser))]
    public virtual ICollection<Debt> Debts { get; set; }
    [InverseProperty(nameof(LedgerRecord.CreditorUser))]
    public virtual ICollection<LedgerRecord> CreditorLedgerRecords { get; set; }
    [InverseProperty(nameof(LedgerRecord.DebtorUser))]
    public virtual ICollection<LedgerRecord> DebtorLedgerRecords { get; set; }
    [InverseProperty(nameof(Spending.User))]
    public virtual ICollection<Spending> Spendings { get; set; }
    [InverseProperty(nameof(NotificationSubscription.User))]
    public virtual ICollection<NotificationSubscription> Subscriptions { get; set; }
    [InverseProperty(nameof(RefreshToken.User))]
    public virtual ICollection<RefreshToken> RefreshTokens { get; set; }

    [Timestamp]
    public byte[] ModifiedAt { get; set; }
    public long Version { get; set; }
}