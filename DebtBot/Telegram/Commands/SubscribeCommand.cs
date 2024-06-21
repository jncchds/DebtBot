using DebtBot.Interfaces.Services;
using DebtBot.Interfaces.Telegram;
using DebtBot.Messages;
using DebtBot.Messages.Notification;
using DebtBot.Models.User;
using MassTransit;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace DebtBot.Telegram.Commands;

public class SubscribeCommand : ITelegramCallbackQuery, ITelegramCommand
{
    private readonly IUserService _userService;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ISubscriptionService _subscriptionService;

    public SubscribeCommand(IUserService userService, IPublishEndpoint publishEndpoint, ISubscriptionService subscriptionService)
    {
        _userService = userService;
        _publishEndpoint = publishEndpoint;
        _subscriptionService = subscriptionService;
    }

    public string CommandName => "/Subscribe";

    public async Task<string?> ExecuteAsync(CallbackQuery query, ITelegramBotClient botClient, CancellationToken cancellationToken)
    {
        string[]? parts = query?.Data?.Split(' ');
        if (parts == null || parts.Length < 3)
        {
            return "Invalid command";
        }
        Guid subscriberId;
        if (!Guid.TryParse(parts[2], out subscriberId))
        {
            return "Invalid user ID";
        }
        Guid? subscribeTargetUserId = _userService.FindUser(new UserSearchModel { TelegramId = query.From.Id })?.Id;
        if (subscribeTargetUserId == null)
        {
            return "User not found";
        }
        if (parts[1] == "Accept")
        {
            _subscriptionService.ConfirmSubscription(subscriberId, subscribeTargetUserId.Value);
        }
        if (parts[1] == "Decline")
        {
            _subscriptionService.RemoveSubscription(subscriberId, subscribeTargetUserId.Value);
        }

        return null;
    }

    public async Task ExecuteAsync(ProcessedMessage processedMessage, ITelegramBotClient botClient, CancellationToken cancellationToken)
    {
        Guid? userId;
        if (Guid.TryParse(processedMessage.ProcessedText, out var result))
        {
            userId = result;
        }
        else
        {
            var searchUser = processedMessage.UserSearchModels.FirstOrDefault();
            if (searchUser == null)
            {
                await _publishEndpoint.Publish(new SendTelegramMessage(processedMessage.ChatId, "User not found"));
                return;
            }
            var user = _userService.FindUser(searchUser);
            if (user == null)
            {
                await _publishEndpoint.Publish(new SendTelegramMessage(processedMessage.ChatId, "User not found"));
                return;
            }
            userId = user.Id;
        }
        
        var fromUserId = _userService.FindUser(new UserSearchModel { TelegramId = processedMessage.FromId })?.Id;

        _subscriptionService.RequestSubscription(fromUserId!.Value, userId.Value);

        await _publishEndpoint.Publish(new SendSubscriptionNotification { SubscriberId = fromUserId!.Value, UserId = userId.Value });
    }
}
