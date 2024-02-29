using DebtBot.Extensions;
using DebtBot.Interfaces.Services;
using DebtBot.Interfaces.Telegram;
using DebtBot.Services;
using Microsoft.Extensions.Options;
using System.Text;
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
    private readonly TelegramConfiguration _telegramConfig;

    public ShowBillsCommand(
		IBillService billService,
		ITelegramService telegramService,
		IOptions<DebtBotConfiguration> debtBotConfig)
	{
		_billService = billService;
		_telegramService = telegramService;
		_telegramConfig = debtBotConfig.Value.Telegram;
	}

	public async Task ExecuteAsync(
		CallbackQuery query,
		ITelegramBotClient botClient,
		CancellationToken cancellationToken)
    {
        int pageNumber = 0;
        int countPerPage = _telegramConfig.CountPerPage;
		int? messageId = null;
        var parametrs = query.Data!.Split(' ');
        if (parametrs.Length > 1)
        {
            _ = int.TryParse(parametrs[1], out pageNumber);
            messageId = query.Message!.MessageId;
        }
        
		var telegramId = query.From.Id;
		var chatId = query.Message!.Chat.Id;
			
		await ShowBills(telegramId, chatId, botClient, pageNumber, countPerPage, messageId, cancellationToken);

		await botClient.AnswerCallbackQueryAsync(query.Id, cancellationToken: cancellationToken);
	}

	public async Task ExecuteAsync(ProcessedMessage processedMessage, ITelegramBotClient botClient, CancellationToken cancellationToken)
	{
		await ShowBills(
			processedMessage.FromId, 
			processedMessage.ChatId, 
			botClient,
			0,
			_telegramConfig.CountPerPage,
			null,
			cancellationToken);
	}

	private async Task ShowBills(long telegramId, long chatId, ITelegramBotClient botClient,
		int pageNumber, int? countPerPage, int? messageId, CancellationToken cancellationToken)
    {
        var userId = _telegramService.GetUserByTelegramId(telegramId);
        if (userId is null)
        {
            await botClient.SendTextMessageAsync(chatId, "User not detected", cancellationToken: cancellationToken);
            return;
        }

        var billsPage = _billService.GetForUser(userId.Value, pageNumber, countPerPage);

		var buttons = new List<InlineKeyboardButton>();

		var sb = new StringBuilder();
		sb.AppendLine("<b>Bills:</b>");
		sb.AppendLine();
		int i = pageNumber * (countPerPage ?? 0);
		billsPage.Items.ForEach(q =>
		{
			sb.AppendLine($"<b>{++i}.</b> {q.Date} by {q.Creator}\n{q.Description}\n");
			buttons.Add(InlineKeyboardButton.WithCallbackData(i.ToString(), ShowBillCommand.FormatCallbackData(q.Id)));
		});

        await botClient.SendOrUpdateTelegramMessage(chatId, messageId, sb.ToString(), [buttons, billsPage.ToInlineKeyboardButtons(CommandString)], cancellationToken);
    }
}
