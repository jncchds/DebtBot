using DebtBot.Interfaces.Services;
using DebtBot.Messages;
using DebtBot.Messages.Notification;
using MassTransit;

namespace DebtBot.Consumers.Notification;

public class SubscriptionRequestedConsumer : IConsumer<SubscriptionRequested>
{
    private readonly IUserService _userService;
    private readonly IPublishEndpoint _publishEndpoint;

    public SubscriptionRequestedConsumer(IUserService userService, IPublishEndpoint publishEndpoint)
    {
        _userService = userService;
        _publishEndpoint = publishEndpoint;
    }

    public async Task Consume(ConsumeContext<SubscriptionRequested> context)
    {
        var subscriptionMessage = context.Message;

        var subscriber = _userService.GetUserById(subscriptionMessage.SubscriberId);
        var user = _userService.GetUserById(subscriptionMessage.UserId);

        TelegramMessageRequested telegramMessage;

        if (user == null || !user.TelegramBotEnabled || !user.TelegramId.HasValue)
        {
            telegramMessage = new TelegramMessageRequested(
                subscriber.TelegramId!.Value,
                "User doesn't use telegram bot. You might need to ask Administrator to enable the subscription");
        }
        else
        {
            var text = $"{subscriber} wants to subscribe";

            telegramMessage = new TelegramMessageRequested(
                user.TelegramId.Value,
                text,
                [new() { new("Accept", $"/Subscribe Accept {user.Id}"), new("Decline", $"/Subscribe Decline {user.Id}") }]
                );
        }

        await _publishEndpoint.Publish(telegramMessage, context.CancellationToken);
    }
}
