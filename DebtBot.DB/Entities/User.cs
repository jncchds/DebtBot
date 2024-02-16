using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using DebtBot.Models.Enums;

namespace DebtBot.DB.Entities;

public class User
{
    [Key]
    public Guid Id { get; set; }
    
    public string DisplayName { get; set; }
    public long? TelegramId { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public UserRole Role { get; set; }

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

    [Timestamp]
    public byte[] ModifiedAt { get; set; }
    public long Version { get; set; }
}