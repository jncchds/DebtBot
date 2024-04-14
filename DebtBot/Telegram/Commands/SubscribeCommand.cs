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
    private readonly IUserContactService _userContactService;

    public SubscribeCommand(IUserService userService, IPublishEndpoint publishEndpoint, IUserContactService userContactService)
    {
        _userService = userService;
        _publishEndpoint = publishEndpoint;
        _userContactService = userContactService;
    }

    public string CommandName => "/Subscribe";

    public async Task<string?> ExecuteAsync(CallbackQuery query, ITelegramBotClient botClient, CancellationToken cancellationToken)
    {
        string[]? parts = query?.Data?.Split(' ');
        if (parts == null || parts.Length < 3)
        {
            return "Invalid command";
        }
        Guid userId;
        if (!Guid.TryParse(parts[2], out userId))
        {
            return "Invalid user ID";
        }
        Guid? fromUserId = _userService.FindUser(new UserSearchModel { TelegramId = query.From.Id })?.Id;
        if (fromUserId == null)
        {
            return "User not found";
        }
        if (parts[1] == "Accept")
        {
            _userContactService.ConfirmSubscription(fromUserId.Value, userId);
        }
        if (parts[1] == "Decline")
        {
            _userContactService.DeclineSubscription(fromUserId.Value, userId);
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

        await _publishEndpoint.Publish(new SendSubscriptionNotification { SubscriberId = fromUserId!.Value, UserId = userId.Value });
    }
}
