using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DebtBot.DB.Entities;

public class Debt
{
    [Key]
    public Guid Id { get; set; }
    public Guid CreditorUserId { get; set; }
    public Guid DebtorUserId { get; set; }

    [Column(TypeName = "decimal(10, 4)")]
    public decimal Amount { get; set; }
    [MaxLength(3)]
    public string CurrencyCode { get; set; }

    [ForeignKey(nameof(CreditorUserId))]
    public virtual User CreditorUser { get; set; }
    [ForeignKey(nameof(DebtorUserId))]
    public virtual User DebtorUser { get; set; }
}