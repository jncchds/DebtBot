using DebtBot.Extensions;
using DebtBot.Interfaces.Services;
using DebtBot.Interfaces.Telegram;
using DebtBot.Services;
using Microsoft.Extensions.Options;
using System.Text;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace DebtBot.Telegram.Commands;

public class DebtsCallbackQuery : ITelegramCallbackQuery, ITelegramCommand
{
    private readonly IDebtService _debtService;
    private readonly ITelegramService _telegramService;
    private readonly TelegramConfiguration _telegramConfig;

    public DebtsCallbackQuery(
        IDebtService debtService,
        ITelegramService telegramService,
        IOptions<DebtBotConfiguration> debtBotConfig)
    {
        _debtService = debtService;
        _telegramService = telegramService;
        _telegramConfig = debtBotConfig.Value.Telegram;
    }

    public const string CommandString = "/Debts";
    public string CommandName => CommandString;

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

        await ShowDebts(query.From.Id, query.Message!.Chat.Id, botClient, pageNumber, countPerPage, messageId, cancellationToken);

        await botClient.AnswerCallbackQueryAsync(query.Id, cancellationToken: cancellationToken);
    }

    public async Task ExecuteAsync(ProcessedMessage processedMessage, ITelegramBotClient botClient, CancellationToken cancellationToken)
    {
        await ShowDebts(processedMessage.FromId, processedMessage.ChatId, botClient, 0, _telegramConfig.CountPerPage, null, cancellationToken);
    }

    private async Task ShowDebts(long telegramId, long chatId, ITelegramBotClient botClient,
        int pageNumber, int? countPerPage, int? messageId, CancellationToken cancellationToken)
    {
        var user = _telegramService.GetUserByTelegramId(telegramId);

        var debts = _debtService.GetForUser(user!.Value, pageNumber, countPerPage);
        var sb = new StringBuilder();
        sb.AppendLine("<b>Debts:</b>");
        sb.AppendLine();
        foreach (var item in debts.Items)
        {
            sb.AppendLine(item.ToCreditorString());
        }

        await botClient.SendOrUpdateTelegramMessage(chatId, messageId, sb.ToString(), [debts.ToInlineKeyboardButtons(CommandString)], cancellationToken);
    }
}
