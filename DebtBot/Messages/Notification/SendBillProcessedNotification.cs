namespace DebtBot.Messages.Notification;

public class SendBillProcessedNotification : SendNotificationBase
{
    public override NotificationType NotificationType => NotificationType.BillProcessed;
    public Guid BillId { get; set; }
}
