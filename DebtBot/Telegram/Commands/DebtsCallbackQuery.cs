using DebtBot.Extensions;
using DebtBot.Interfaces.Services;
using DebtBot.Interfaces.Telegram;
using DebtBot.Messages;
using DebtBot.Services;
using MassTransit;
using Microsoft.Extensions.Options;
using System.Text;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace DebtBot.Telegram.Commands;

public class DebtsCallbackQuery : ITelegramCallbackQuery, ITelegramCommand
{
    private readonly IDebtService _debtService;
    private readonly ITelegramService _telegramService;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly TelegramConfiguration _telegramConfig;

    public DebtsCallbackQuery(
        IDebtService debtService,
        ITelegramService telegramService,
        IOptions<DebtBotConfiguration> debtBotConfig,
        IPublishEndpoint publishEndpoint)
    {
        _debtService = debtService;
        _telegramService = telegramService;
        _publishEndpoint = publishEndpoint;
        _telegramConfig = debtBotConfig.Value.Telegram;
    }

    public const string CommandString = "/Debts";
    public string CommandName => CommandString;

    public async Task<string?> ExecuteAsync(
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

        return null;
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
        var buttons = new List<InlineButtonRecord>();
        var sb = new StringBuilder();
        int debtIndex = debts.TotalCount - pageNumber * (countPerPage ?? 0);
        
        sb.AppendLine("<b>Debts:</b>");
        foreach (var debtGroup in debts.Items.GroupBy(t => t.CreditorUser.Id))
        {
            sb.AppendLine();
            sb.AppendLine($"To {debtGroup.First().CreditorUser.ToString()}:");
            foreach (var debt in debtGroup)
            {
                sb.AppendLine($"  <b>{debtIndex}.</b> {debt.DebtorUser.ToString()} owes {debt.Amount:0.##} {debt.CurrencyCode}");
                buttons.Add(new(debtIndex.ToString(), BillsCommand.FormatCallbackData(debt.DebtorUser.Id, debt.CurrencyCode, true)));
                debtIndex--;
            }
        }

        await _publishEndpoint.Publish(
            new SendTelegramMessage(
                chatId, 
                sb.ToString(), 
                InlineKeyboard: [buttons, debts.ToInlineKeyboardButtons(CommandString)],
                MessageId: messageId
            ));
    }
}
