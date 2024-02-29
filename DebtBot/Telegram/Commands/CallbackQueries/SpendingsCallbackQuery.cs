using DebtBot.Extensions;
using DebtBot.Interfaces.Services;
using DebtBot.Interfaces.Telegram;
using DebtBot.Services;
using Microsoft.Extensions.Options;
using System.Text;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace DebtBot.Telegram.Commands.CallbackQueries;

public class SpendingsCallbackQuery : ITelegramCallbackQuery
{
	private readonly ITelegramService _telegramService;
	private readonly IBudgetService _budgetService;
    private readonly TelegramConfiguration _telegramConfig;

    public SpendingsCallbackQuery(
		ITelegramService telegramService,
		IBudgetService budgetService,
		IOptions<DebtBotConfiguration> debtBotConfig)
	{
		_telegramService = telegramService;
		_budgetService = budgetService;
        _telegramConfig = debtBotConfig.Value.Telegram;
    }

	public const string CommandString = "/Spendings";
	public string CommandName => CommandString;
	
	public async Task ExecuteAsync(
		CallbackQuery query,
		ITelegramBotClient botClient,
		CancellationToken cancellationToken)
    {
        int pageNumber = 0;
        int? countPerPage = _telegramConfig.CountPerPage;
		int? messageId = null;
        var parametrs = query.Data!.Split(' ');
        if (parametrs.Length > 1)
        {
            _ = int.TryParse(parametrs[1], out pageNumber);
			messageId = query.Message!.MessageId;
        }
        
		var user = _telegramService.GetUserByTelegramId(query.From.Id);

		var spendingsPage = _budgetService.GetSpendings(user!.Value, pageNumber, countPerPage);

		var buttons = new List<InlineKeyboardButton>();

		var sb = new StringBuilder();
		sb.AppendLine("<b>Spendings:</b>");
		sb.AppendLine();
		int i = pageNumber * (countPerPage ?? 0);
		foreach (var spending in spendingsPage.Items)
		{
			sb.Append($"<b>{++i}</b>. ");
			sb.AppendLine(spending.ToString());
			sb.AppendLine();
			buttons.Add(InlineKeyboardButton.WithCallbackData(i.ToString(), $"{ShowBillCommand.CommandString} {spending.BillId}"));
        }

		await botClient.SendOrUpdateTelegramMessage(query.Message!.Chat.Id, messageId, sb.ToString(), [ buttons, spendingsPage.ToInlineKeyboardButtons(CommandString)], cancellationToken);
				
		await botClient.AnswerCallbackQueryAsync(query.Id, cancellationToken: cancellationToken);
	}
}