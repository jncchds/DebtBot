using System.ComponentModel.DataAnnotations;

namespace DebtBot.DB.Entities;

public class Bill
{
    [Key]
    public Guid Id { get; set; }
    
    public string CurrencyCode { get; set; }
    public string Description { get; set; }
    public DateTime Date { get; set; }
    
    public virtual ICollection<BillLine> Lines { get; set; }
    public virtual ICollection<BillPayment> Payments { get; set; }
}