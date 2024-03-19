namespace DebtBot.Messages.Notification;

public class SendShowBillNotificationMessage : SendNotificationBaseMessage
{
    public override NotificationType NotificationType => NotificationType.ShowBill;
    public Guid BillId { get; set; }
    public long ChatId { get; set; }
}
