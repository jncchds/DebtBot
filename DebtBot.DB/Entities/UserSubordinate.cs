using System.ComponentModel.DataAnnotations.Schema;

namespace DebtBot.DB.Entities;

public class UserSubordinate
{
    public Guid UserId { get; set; }
    public Guid SubordinateUserId { get; set; }
    public string DisplayName { get; set; }

    [ForeignKey(nameof(UserId))]
    public virtual User User { get; set; }

    [ForeignKey(nameof(SubordinateUserId))]
    public virtual User SubordinateUser { get; set; }
}