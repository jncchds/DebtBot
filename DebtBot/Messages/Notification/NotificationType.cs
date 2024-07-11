namespace DebtBot.Messages.Notification;

public enum NotificationType : byte
{
    None = 0,
    BillProcessed = 1,
    Bill = 2,
    Subscription = 3,
    Exchange = 4
}
