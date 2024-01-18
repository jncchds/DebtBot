using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DebtBot.DB.Entities;

public class BillLine
{
    [Key]
    public Guid Id { get; set; }
    public Guid BillId { get; set; }
    
    public string ItemDescription { get; set; }
    public decimal Subtotal { get; set; }
    
    [ForeignKey(nameof(BillId))]
    public virtual Bill Bill { get; set; }
    public virtual ICollection<BillLineParticipant> Participants { get; set; }
}