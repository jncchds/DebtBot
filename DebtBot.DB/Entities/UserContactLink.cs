using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;

namespace DebtBot.DB.Entities;

[PrimaryKey(nameof(UserId), nameof(ContactUserId))]
public class UserContactLink
{
    public Guid UserId { get; set; }
    public Guid ContactUserId { get; set; }
    
    public string DisplayName { get; set; }

    /// <summary>When the link was created (UTC). Null for links made before this column existed.</summary>
    public DateTime? AddedAt { get; set; }

    [ForeignKey(nameof(UserId))]
    public virtual User User { get; set; }
    [ForeignKey(nameof(ContactUserId))]
    public virtual User ContactUser { get; set; }
	    
	    
}