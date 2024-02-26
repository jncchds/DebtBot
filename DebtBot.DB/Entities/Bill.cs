using DebtBot.Models.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DebtBot.DB.Entities;

public class Bill
{
    [Key]
    public Guid Id { get; set; }
    public Guid CreatorId { get; set; }
    public string CurrencyCode { get; set; }
    public string PaymentCurrencyCode { get; set; }
    [Column(TypeName = "decimal(10, 4)")]
    public decimal TotalWithTips { get; set; }
    public string Description { get; set; }
    public DateTime Date { get; set; }
    public ProcessingState Status { get; set; }
    
    public virtual ICollection<BillLine> Lines { get; set; }
    public virtual ICollection<BillPayment> Payments { get; set; }
    [InverseProperty(nameof(Spending.Bill))]
    public virtual ICollection<Spending> Spendings { get; set; }
    [ForeignKey(nameof(CreatorId))]
    public virtual User Creator { get; set; }
}