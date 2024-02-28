using DebtBot.Interfaces.Services;
using DebtBot.Interfaces.Telegram;
using DebtBot.Services;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace DebtBot.Telegram.Commands;

public class ShowBillsCommand: ITelegramCommand, ITelegramCallbackQuery
{
	public const string CommandString = "/ShowBills";
	public string CommandName => CommandString;

	private readonly IBillService _billService;
	private readonly ITelegramService _telegramService;

	public ShowBillsCommand(IBillService billService, ITelegramService telegramService)
	{
		_billService = billService;
		_telegramService = telegramService;
	}

	public async Task ExecuteAsync(CallbackQuery query, ITelegramBotClient botClient, CancellationToken cancellationToken)
	{
		var telegramId = query.From.Id;
		var chatId = query.Message!.Chat.Id;
			
		await ShowBills(telegramId, chatId, botClient, cancellationToken);

		await botClient.AnswerCallbackQueryAsync(query.Id, cancellationToken: cancellationToken);
	}

	public async Task ExecuteAsync(ProcessedMessage processedMessage, ITelegramBotClient botClient, CancellationToken cancellationToken)
	{
		await ShowBills(
			processedMessage.FromId, 
			processedMessage.ChatId, 
			botClient,
			cancellationToken);
	}
	
	private async Task ShowBills(long telegramId, long chatId, ITelegramBotClient botClient,
		CancellationToken cancellationToken)
	{
		var userId = _telegramService.GetUserByTelegramId(telegramId);
		if (userId is null)
		{
			await botClient.SendTextMessageAsync(chatId, "User not detected", cancellationToken: cancellationToken);
			return;
		}

		var bills = _billService.GetForUser(userId.Value);

		await botClient.SendTextMessageAsync(
			chatId,
			"Bills:",
			replyMarkup: new InlineKeyboardMarkup(
				bills.Select(q => new[]{ InlineKeyboardButton.WithCallbackData(q.Description, $"{ShowBillCommand.CommandString} {q.Id}")})
			),
			cancellationToken: cancellationToken);
	}
}
