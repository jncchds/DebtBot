using DebtBot.Interfaces.Services;
using DebtBot.Models.User;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace DebtBot.Telegram.Commands;

public class StartCommand : ITelegramCommand
{
    private readonly IUserService _userService;

    public StartCommand(IUserService userService)
    {
        _userService = userService;
    }

    public string CommandName => "/Start";

    public async Task ExecuteAsync(Message message, ITelegramBotClient botClient, CancellationToken cancellationToken)
    {
        if (message.Chat.Type == ChatType.Channel)
        {
            await botClient.SendTextMessageAsync(message.Chat.Id, $"Hello! Currently channels are not supported", cancellationToken: cancellationToken);
            return;
        }

        var user = message.From!;

        var userModel = _userService.FindUser(new UserSearchModel() { TelegramId = user.Id });

        await botClient.SendTextMessageAsync(message.Chat.Id, $"Hello, {userModel!.DisplayName}!", cancellationToken: cancellationToken);
    }
}