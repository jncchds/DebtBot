using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DebtBot.DB.Entities;

[PrimaryKey(nameof(SubscriberId), nameof(UserId))]
public class NotificationSubscription
{
    public Guid SubscriberId { get; set; }
    public Guid UserId { get; set; }
    public bool IsConfirmed { get; set; }

    [ForeignKey(nameof(SubscriberId))]
    public User Subscriber { get; set; }
    [ForeignKey(nameof(UserId))]
    public User User { get; set; }
}
