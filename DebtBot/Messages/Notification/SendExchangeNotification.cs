namespace DebtBot.Messages.Notification;

public class SendExchangeNotification : SendNotificationBase
{
    public override NotificationType NotificationType => NotificationType.Exchange;
    public Guid ForwardBillId { get; set; }
    public Guid BackwardBillId { get; set; }
    public long ChatId { get; set; }
}
