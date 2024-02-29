using System.Text;
using DebtBot.Extensions;
using DebtBot.Interfaces.Services;
using DebtBot.Interfaces.Telegram;
using DebtBot.Services;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace DebtBot.Telegram.Commands.CallbackQueries;

public class DebtsCallbackQuery : ITelegramCallbackQuery
{
    private readonly IDebtService _debtService;
    private readonly ITelegramService _telegramService;

    public DebtsCallbackQuery(IDebtService debtService,
        ITelegramService telegramService)
    {
        _debtService = debtService;
        _telegramService = telegramService;
    }

    public const string CommandString = "/Debts";
    public string CommandName => CommandString;

    public async Task ExecuteAsync(
        CallbackQuery query,
        ITelegramBotClient botClient,
        CancellationToken cancellationToken)
    {
        int pageNumber = 0;
        int countPerPage = 5;
        bool updateMessage = false;
        var parametrs = query.Data!.Split(' ');
        if (parametrs.Length > 1)
        {
            Int32.TryParse(parametrs[1], out pageNumber);
            updateMessage = true;
        }

        var user = _telegramService.GetUserByTelegramId(query.From.Id);

        var debts = _debtService.GetForUser(user!.Value, pageNumber, countPerPage);
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("<b>Debts:</b>");
        sb.AppendLine();
        foreach (var item in debts.Items)
        {
            sb.AppendLine(item.ToCreditorString());
        }

        if (updateMessage)
        {
            await botClient.EditMessageTextAsync(
                query.Message!.Chat.Id,
                query.Message.MessageId,
                sb.ToString(),
                parseMode: ParseMode.Html,
                replyMarkup: new InlineKeyboardMarkup(debts.ToInlineKeyboardButtons(CommandString)),
                cancellationToken: cancellationToken);
        }
        else
        {
            await botClient.SendTextMessageAsync(
                query.Message!.Chat.Id,
                sb.ToString(),
                parseMode: ParseMode.Html,
                replyMarkup: new InlineKeyboardMarkup(debts.ToInlineKeyboardButtons(CommandString)),
                cancellationToken: cancellationToken);
        }

        await botClient.AnswerCallbackQueryAsync(query.Id, cancellationToken: cancellationToken);
    }
}
