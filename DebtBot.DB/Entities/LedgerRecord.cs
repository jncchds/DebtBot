using System.ComponentModel.DataAnnotations.Schema;

namespace DebtBot.DB.Entities;

public class LedgerRecord
{
    public Guid CreditorUserId { get; set; }
    public Guid DebtorUserId { get; set; }
    public Guid BillId { get; set; }
    
    [Column(TypeName = "decimal(10, 4)")]
    public decimal Amount { get; set; }
    public string Currency { get; set; }
    
    [ForeignKey(nameof(CreditorUserId))]
    public virtual User CreditorUser { get; set; }
    [ForeignKey(nameof(DebtorUserId))]
    public virtual User DebtorUser { get; set; }
    [ForeignKey(nameof(BillId))]
    public virtual Bill Bill { get; set; }
}