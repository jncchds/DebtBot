using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DebtBot.DB.Entities;

public class BillLineParticipant
{
    [Key]
    public Guid BillLineId { get; set; }
    public Guid UserId { get; set; }
    
    [Column(TypeName = "decimal(10, 4)")]
    public decimal Part { get; set; }

    [ForeignKey(nameof(BillLineId))]
    public virtual BillLine BillLine { get; set; }
    [ForeignKey(nameof(UserId))]
    public virtual User User { get; set; }
}