using DebtBot.Interfaces.Services;
using DebtBot.Interfaces.Telegram;
using DebtBot.Services;
using System.Text;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace DebtBot.Telegram.CallbackQueries;

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

    public string CommandName => "/Debts";

    public async Task ExecuteAsync(
        CallbackQuery query,
        ITelegramBotClient botClient,
        CancellationToken cancellationToken)
    {
        var user = _telegramService.GetUserByTelegramId(query.From.Id);

        var debts = _debtService.Get(user!.Value);
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("<b>Debts:</b>");
        sb.AppendLine();
        foreach (var item in debts)
        {
            sb.AppendLine(item.ToCreditorString());
        }

        await botClient.SendTextMessageAsync(query.Message!.Chat.Id, sb.ToString(), cancellationToken: cancellationToken, parseMode: ParseMode.Html);

        await botClient.AnswerCallbackQueryAsync(query.Id);
    }
}
