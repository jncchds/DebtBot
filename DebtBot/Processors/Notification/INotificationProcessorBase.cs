using DebtBot.Messages.Notification;

namespace DebtBot.Processors.Notification;

public interface INotificationProcessorBase
{
    NotificationType NotificationType { get; }

    Task Process(SendNotificationBase message);
}
