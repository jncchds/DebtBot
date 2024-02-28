using System.Text;
using DebtBot.Interfaces.Services;
using DebtBot.Interfaces.Telegram;
using DebtBot.Services;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace DebtBot.Telegram.Commands.CallbackQueries;

public class SpendingsCallbackQuery : ITelegramCallbackQuery
{
	private readonly ITelegramService _telegramService;
	private readonly IBudgetService _budgetService;

	public SpendingsCallbackQuery(ITelegramService telegramService, IBudgetService budgetService)
	{
		_telegramService = telegramService;
		_budgetService = budgetService;
	}

	public const string CommandString = "/Spendings";
	public string CommandName => CommandString;
	
	public async Task ExecuteAsync(
		CallbackQuery query,
		ITelegramBotClient botClient,
		CancellationToken cancellationToken)
	{
		var user = _telegramService.GetUserByTelegramId(query.From.Id);

		var spendings = _budgetService.GetSpendings(user!.Value);
		
		StringBuilder sb = new StringBuilder();
		sb.AppendLine("<b>Spendings:</b>");
		sb.AppendLine();
		foreach (var spending in spendings)
		{
			sb.AppendLine(spending.ToString());
		}
		
		await botClient.SendTextMessageAsync(query.Message!.Chat.Id, sb.ToString(), cancellationToken: cancellationToken, parseMode: ParseMode.Html);
		
		await botClient.AnswerCallbackQueryAsync(query.Id, cancellationToken: cancellationToken);
	}
}