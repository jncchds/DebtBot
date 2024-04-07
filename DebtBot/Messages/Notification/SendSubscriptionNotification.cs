namespace DebtBot.Messages.Notification;

public class SendSubscriptionNotification : SendNotificationBase
{
    public override NotificationType NotificationType => NotificationType.Subscription;
    public Guid UserId { get; set; }
    public Guid SubscriberId { get; set; }
}
