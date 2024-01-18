﻿using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DebtBot.DB.Entities;

public class User
{
    [Key]
    public Guid Id { get; set; }
    public string DisplayName { get; set; }
    public long? TelegramId { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }

    [InverseProperty(nameof(UserSubordinate.User))]
    public virtual ICollection<UserSubordinate> UserSubordinates { get; set; }
    [InverseProperty(nameof(Debt.CreditorUser))]
    public virtual ICollection<Debt> Debts { get; set; }
    [InverseProperty(nameof(LedgerRecord.CreditorUser))]
    public virtual ICollection<LedgerRecord> CreditorLedgerRecords { get; set; }
    [InverseProperty(nameof(LedgerRecord.DebtorUser))]
    public virtual ICollection<LedgerRecord> DebtorLedgerRecords { get; set; }
}