using DebtBot.Messages.Notification;
using MassTransit;

namespace DebtBot.Processors.Notification;

public class SendNotificationConsumer : IConsumer<SendNotificationBaseMessage>
{
    private readonly IEnumerable<Notification.INotificationProcessorBase> _notificationProcessors;

    public SendNotificationConsumer(IEnumerable<INotificationProcessorBase> notificationProcessors)
    {
        _notificationProcessors = notificationProcessors;
    }

    public Task Consume(ConsumeContext<SendNotificationBaseMessage> context)
    {
        var message = context.Message;
        
        var processor = _notificationProcessors.FirstOrDefault(x => x.NotificationType == message.NotificationType);
        if (processor == null)
        {
            Console.WriteLine($"No processor found for notification type: {message.NotificationType}");
            return Task.CompletedTask;
        }

        processor.Process(message);

        return Task.CompletedTask;
    }
}
