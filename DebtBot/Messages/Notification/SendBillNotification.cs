namespace DebtBot.Messages.Notification;

public class SendBillNotification : SendNotificationBase
{
    public override NotificationType NotificationType => NotificationType.Bill;
    public Guid BillId { get; set; }
    public long ChatId { get; set; }
}
