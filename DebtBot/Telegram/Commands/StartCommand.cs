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

    public async Task ExecuteAsync(
	    ProcessedMessage processedMessage,
	    ITelegramBotClient botClient,
	    CancellationToken cancellationToken)
    {
	    var userModel = _userService.FindUser(new UserSearchModel { TelegramId = processedMessage.FromId });
	    await botClient.SendTextMessageAsync(
		    processedMessage.ChatId, 
		    $"Hello, {userModel!.DisplayName}!",
		    cancellationToken: cancellationToken);
    }
}