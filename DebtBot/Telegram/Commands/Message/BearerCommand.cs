using DebtBot.Interfaces;
using DebtBot.Interfaces.Services;
using DebtBot.Interfaces.Telegram;
using DebtBot.Messages;
using DebtBot.Models.User;
using DebtBot.Services;
using MassTransit;
using Telegram.Bot;

namespace DebtBot.Telegram.Commands.Message;

public class BearerCommand : ITelegramCommand
{
    private readonly IUserService _userService;
    private readonly IIdentityService _identityService;
    private readonly ITelegramService _telegramService;
    private readonly IPublishEndpoint _publishEndpoint;

    public BearerCommand(IUserService userService, IIdentityService identityService, ITelegramService telegramService, IPublishEndpoint publishEndpoint)
    {
        _userService = userService;
        _identityService = identityService;
        _telegramService = telegramService;
        _publishEndpoint = publishEndpoint;
    }

    public string CommandName => "/Bearer";

    public async Task ExecuteAsync(ProcessedMessage processedMessage, ITelegramBotClient botClient, CancellationToken cancellationToken)
    {
        UserModel? user;
        if (processedMessage.UserSearchModels.Count > 0)
        {
            user = _userService.FindUser(processedMessage.UserSearchModels.First());
        }
        else
        {
            user = _userService.FindUser(new Models.User.UserSearchModel { TelegramId = processedMessage.FromId });
        }

        if (user == null)
        {
            await _publishEndpoint.Publish(new TelegramMessageRequested(processedMessage.ChatId, "User not found"));
            return;
        }
        var token = _identityService.GenerateJwtToken(user!);
        await _publishEndpoint.Publish(new TelegramMessageRequested(processedMessage.ChatId, $"Your bearer token: <code>{token}</code>"));
    }
}
