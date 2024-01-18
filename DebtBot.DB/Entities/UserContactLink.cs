using System.ComponentModel.DataAnnotations.Schema;

namespace DebtBot.DB.Entities;

public class UserContactLink
{
    public Guid UserId { get; set; }
    public Guid ContactUserId { get; set; }
    
    public string DisplayName { get; set; }

    [ForeignKey(nameof(UserId))]
    public virtual User User { get; set; }
    [ForeignKey(nameof(ContactUserId))]
    public virtual User ContactUser { get; set; }
	    
	    
}