using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace DebtBot.DB.Entities;

[PrimaryKey(nameof(BillId), nameof(UserId))]
public class Spending
{
	public Guid BillId { get; set; }
	public Guid UserId { get; set; }
	
	public string Description { get; set; }
	
    [Column(TypeName = "decimal(10, 4)")]
    public decimal Amount { get; set; }
    public string CurrencyCode { get; set; }
    
    [Column(TypeName = "decimal(10, 4)")]
    public decimal PaymentAmount { get; set; }
    public string PaymentCurrencyCode { get; set; }

    [Column(TypeName = "decimal(10, 4)")]
    public decimal Portion { get; set; }
    public DateTime Date { get; set; }
    public bool IsCanceled { get; set; }

    [ForeignKey(nameof(BillId))]
    public virtual Bill Bill { get; set; }

    [DeleteBehavior(DeleteBehavior.Restrict)]
    [ForeignKey(nameof(UserId))]
    public virtual User User { get; set; }
}