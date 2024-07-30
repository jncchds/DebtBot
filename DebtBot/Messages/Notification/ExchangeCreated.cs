namespace DebtBot.Messages.Notification;

public class ExchangeCreated
{
    public Guid ForwardBillId { get; set; }
    public Guid BackwardBillId { get; set; }
    public long ChatId { get; set; }
}
