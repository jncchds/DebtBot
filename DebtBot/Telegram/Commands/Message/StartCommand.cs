using DebtBot.Interfaces.Services;
using DebtBot.Interfaces.Telegram;
using DebtBot.Models.User;
using Telegram.Bot;

namespace DebtBot.Telegram.Commands.Message;

public class StartCommand : ITelegramCommand
{
    private readonly IUserService _userService;

    public StartCommand(IUserService userService)
    {
        _userService = userService;
    }

    public const string CommandString = "/Start";
    public string CommandName => CommandString;

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
	    
		_userService.SetBotActiveState(userModel.Id, true);
    }
}