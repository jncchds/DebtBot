namespace DebtBot.Messages.Notification;

public class SendBillProcessedNotificationMessage : SendNotificationBaseMessage
{
    public override NotificationType NotificationType => NotificationType.BillProcessed;
    public Guid BillId { get; set; }
}
