using DebtBot.Interfaces.Services;
using DebtBot.Interfaces.Telegram;
using DebtBot.Messages;
using DebtBot.Services;
using MassTransit;
using Telegram.Bot;

namespace DebtBot.Telegram.Commands.Message;

public class BearerCommand : ITelegramCommand
{
    private readonly IUserService _userService;
    private readonly ITelegramService _telegramService;
    private readonly IPublishEndpoint _publishEndpoint;

    public BearerCommand(IUserService userService, ITelegramService telegramService, IPublishEndpoint publishEndpoint)
    {
        _userService = userService;
        _telegramService = telegramService;
        _publishEndpoint = publishEndpoint;
    }

    public string CommandName => "/Bearer";

    public async Task ExecuteAsync(ProcessedMessage processedMessage, ITelegramBotClient botClient, CancellationToken cancellationToken)
    {
        var user = _userService.FindUser(new Models.User.UserSearchModel { TelegramId = processedMessage.FromId });
        if (user == null)
        {
            await _publishEndpoint.Publish(new SendTelegramMessage(processedMessage.ChatId, "User not found"));
            return;
        }
        var token = _userService.GenerateJwtToken(user!);
        await _publishEndpoint.Publish(new SendTelegramMessage(processedMessage.ChatId, $"Your bearer token: <code>{token}</code>"));
    }
}
