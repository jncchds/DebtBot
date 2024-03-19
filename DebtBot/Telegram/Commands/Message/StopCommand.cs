using DebtBot.Extensions;
using DebtBot.Interfaces.Services;
using DebtBot.Interfaces.Telegram;
using DebtBot.Messages;
using DebtBot.Models.User;
using MassTransit;
using Telegram.Bot;

namespace DebtBot.Telegram.Commands.Message;

public class StopCommand : ITelegramCommand
{
    private readonly IUserService _userService;
    private readonly IPublishEndpoint _publishEndpoint;

    public StopCommand(IUserService userService,
		IPublishEndpoint publishEndpoint)
    {
        _userService = userService;
		_publishEndpoint = publishEndpoint;
    }

    public const string CommandString = "/Stop";
    public string CommandName => CommandString;

    public async Task ExecuteAsync(
	    ProcessedMessage processedMessage,
	    ITelegramBotClient botClient,
	    CancellationToken cancellationToken)
    {
	    var userModel = _userService.FindUser(new UserSearchModel { TelegramId = processedMessage.FromId });

		await _publishEndpoint.Publish(new SendTelegramMessage(
			processedMessage.ChatId,
			$"Stopped, {userModel!.DisplayName}!",
			ReplyKeyboard: TelegramBotExtensions.DefaultReplyKeyboard
			));

		_userService.SetBotActiveState(userModel.Id, false);
    }
}