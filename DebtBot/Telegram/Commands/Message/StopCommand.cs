using DebtBot.Extensions;
using DebtBot.Interfaces.Services;
using DebtBot.Interfaces.Telegram;
using DebtBot.Models.User;
using Telegram.Bot;

namespace DebtBot.Telegram.Commands.Message;

public class StopCommand : ITelegramCommand
{
    private readonly IUserService _userService;

    public StopCommand(IUserService userService)
    {
        _userService = userService;
    }

    public const string CommandString = "/Stop";
    public string CommandName => CommandString;

    public async Task ExecuteAsync(
	    ProcessedMessage processedMessage,
	    ITelegramBotClient botClient,
	    CancellationToken cancellationToken)
    {
	    var userModel = _userService.FindUser(new UserSearchModel { TelegramId = processedMessage.FromId });
	    
	    await botClient.SendTextMessageAsync(
		    processedMessage.ChatId, 
		    $"Stopped, {userModel!.DisplayName}!",
			replyMarkup: TelegramBotExtensions.DefaultReplyKeyboard,
		    cancellationToken: cancellationToken);
	    
		_userService.SetBotActiveState(userModel.Id, false);
    }
}