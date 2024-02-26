using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;

namespace DebtBot.DB.Entities;

[PrimaryKey(nameof(BillId), nameof(UserId))]
public class BillParticipant
{
    public Guid BillId { get; set; }
    public Guid UserId { get; set; }

    [ForeignKey(nameof(BillId))]
    public virtual Bill Bill { get; set; }
    [ForeignKey(nameof(UserId))]
    public virtual User User { get; set; }
}
