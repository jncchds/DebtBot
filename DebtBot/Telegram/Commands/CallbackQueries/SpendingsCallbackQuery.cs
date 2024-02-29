﻿using DebtBot.Extensions;
using DebtBot.Interfaces.Services;
using DebtBot.Interfaces.Telegram;
using DebtBot.Services;
using System.Text;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

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
        int pageNumber = 0;
        int? countPerPage = 5;
		bool updateMessage = false;
        var parametrs = query.Data!.Split(' ');
        if (parametrs.Length > 1)
        {
            Int32.TryParse(parametrs[1], out pageNumber);
			updateMessage = true;
        }
        
		var user = _telegramService.GetUserByTelegramId(query.From.Id);

		var spendings = _budgetService.GetSpendings(user!.Value, pageNumber, countPerPage);
		
		StringBuilder sb = new StringBuilder();
		sb.AppendLine("<b>Spendings:</b>");
		sb.AppendLine();
		int i = 0;
		foreach (var spending in spendings.Items)
		{
			sb.AppendLine(spending.ToSpendingString());
        }

        var buttons = new List<List<InlineKeyboardButton>>
        {
            spendings.ToInlineKeyboardButtons(CommandString)
        };

        buttons.AddRange(
            spendings.Items.Select(q => new List<InlineKeyboardButton> { InlineKeyboardButton.WithCallbackData(q.Description, $"{ShowBillCommand.CommandString} {q.BillId}") }));

        if (updateMessage)
		{
            await botClient.EditMessageTextAsync(
                query.Message!.Chat.Id,
				query.Message.MessageId,
                sb.ToString(),
                parseMode: ParseMode.Html,
                replyMarkup: new InlineKeyboardMarkup(buttons),
                cancellationToken: cancellationToken);
        }
        else
		{
			await botClient.SendTextMessageAsync(
				query.Message!.Chat.Id,
				sb.ToString(),
				parseMode: ParseMode.Html,
				replyMarkup: new InlineKeyboardMarkup(buttons),
				cancellationToken: cancellationToken);
		}
		
		await botClient.AnswerCallbackQueryAsync(query.Id, cancellationToken: cancellationToken);
	}
}