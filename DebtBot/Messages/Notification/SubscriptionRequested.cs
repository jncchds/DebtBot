namespace DebtBot.Messages.Notification;

public class SubscriptionRequested
{
    public Guid UserId { get; set; }
    public Guid SubscriberId { get; set; }
}
