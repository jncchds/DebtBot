namespace DebtBot.Messages.Notification;

public class BillNotificationRequested
{
    public Guid BillId { get; set; }
    public long ChatId { get; set; }
    public int? MessageId { get; set; }
}
