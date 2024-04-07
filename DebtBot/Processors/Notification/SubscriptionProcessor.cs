using DebtBot.Interfaces.Services;
using DebtBot.Messages;
using DebtBot.Messages.Notification;
using MassTransit;

namespace DebtBot.Processors.Notification;

public class SubscriptionProcessor : INotificationProcessorBase
{
    private readonly IUserService _userService;
    private readonly IPublishEndpoint _publishEndpoint;

    public SubscriptionProcessor(IUserService userService, IPublishEndpoint publishEndpoint)
    {
        _userService = userService;
        _publishEndpoint = publishEndpoint;
    }

    public NotificationType NotificationType => NotificationType.Subscription;

    public async Task Process(SendNotificationBase message)
    {
        var subscriptionMessage = (SendSubscriptionNotification)message;

        var subscriber = _userService.GetUserById(subscriptionMessage.SubscriberId);
        var user = _userService.GetUserById(subscriptionMessage.UserId);

        SendTelegramMessage telegramMessage;

        if (!user.TelegramBotEnabled || !user.TelegramId.HasValue)
        {
            telegramMessage = new SendTelegramMessage(
                subscriber.TelegramId!.Value,
                "User doesn't use telegram bot. You might need to ask Administrator to enable the subscription");
        }
        else
        {
            var text = $"{subscriber} wants to subscribe";

            telegramMessage = new SendTelegramMessage(
                user.TelegramId.Value,
                text,
                [new() { new("Accept", $"/Subscribe Accept {user.Id}"), new("Decline", $"/Subscribe Decline {user.Id}") }]
                );
        }

        await _publishEndpoint.Publish(telegramMessage);
    }
}
