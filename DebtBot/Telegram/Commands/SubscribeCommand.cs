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

    public SubscribeCommand(IUserService userService, IPublishEndpoint publishEndpoint)
    {
        _userService = userService;
        _publishEndpoint = publishEndpoint;
    }

    public string CommandName => "/Subscribe";

    public async Task ExecuteAsync(CallbackQuery query, ITelegramBotClient botClient, CancellationToken cancellationToken)
    {

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
