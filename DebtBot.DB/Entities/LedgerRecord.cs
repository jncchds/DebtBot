using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;

namespace DebtBot.DB.Entities;

[PrimaryKey(nameof(CreditorUserId), nameof(DebtorUserId), nameof(BillId))]
public class LedgerRecord
{
    public Guid CreditorUserId { get; set; }
    public Guid DebtorUserId { get; set; }
    public Guid BillId { get; set; }
    
    [Column(TypeName = "decimal(10, 4)")]
    public decimal Amount { get; set; }
    public string CurrencyCode { get; set; }
    public bool IsCanceled { get; set; }

    [DeleteBehavior(DeleteBehavior.Restrict)]
    [ForeignKey(nameof(CreditorUserId))]
    public virtual User CreditorUser { get; set; }

    [DeleteBehavior(DeleteBehavior.Restrict)]
    [ForeignKey(nameof(DebtorUserId))]
    public virtual User DebtorUser { get; set; }
    [ForeignKey(nameof(BillId))]
    public virtual Bill Bill { get; set; }
}