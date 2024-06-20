using DebtBot.Interfaces.Services;
using DebtBot.Interfaces.Telegram;
using DebtBot.Messages;
using DebtBot.Services;
using MassTransit;
using Microsoft.Extensions.Options;
using System.Text;
using Telegram.Bot.Types;
using Telegram.Bot;
using DebtBot.Extensions;

namespace DebtBot.Telegram.Commands;

public class BillsCommand : ITelegramCommand, ITelegramCallbackQuery
{
    public const string CommandString = "/Bills";
    public string CommandName => CommandString;

    private readonly IBillService _billService;
    private readonly ITelegramService _telegramService;
    private readonly TelegramConfiguration _telegramConfig;
    private readonly IPublishEndpoint _publishEndpoint;

    public BillsCommand(
        IBillService billService,
        ITelegramService telegramService,
        IOptions<DebtBotConfiguration> debtBotConfig,
        IPublishEndpoint publishEndpoint)
    {
        _billService = billService;
        _telegramService = telegramService;
        _telegramConfig = debtBotConfig.Value.Telegram;
        _publishEndpoint = publishEndpoint;
    }

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

        var telegramId = query.From.Id;
        var chatId = query.Message!.Chat.Id;

        var ret = await ShowBills(telegramId, chatId, botClient, pageNumber, countPerPage, messageId, cancellationToken);

        return ret;
    }

    public async Task ExecuteAsync(ProcessedMessage processedMessage, ITelegramBotClient botClient, CancellationToken cancellationToken)
    {
        var ret = await ShowBills(
            processedMessage.FromId,
            processedMessage.ChatId,
            botClient,
            0,
            _telegramConfig.CountPerPage,
            null,
            cancellationToken);

        if (!string.IsNullOrWhiteSpace(ret))
        {
            await _publishEndpoint.Publish(new SendTelegramMessage(processedMessage.ChatId, ret));
        }
    }

    private async Task<string?> ShowBills(long telegramId, long chatId, ITelegramBotClient botClient,
        int pageNumber, int? countPerPage, int? messageId, CancellationToken cancellationToken)
    {
        var userId = _telegramService.GetUserByTelegramId(telegramId);
        if (userId is null)
        {
            return "User not detected";
        }

        var billsPage = _billService.GetForUser(userId.Value, pageNumber, countPerPage);

        var buttons = new List<InlineButtonRecord>();

        var sb = new StringBuilder();
        sb.AppendLine("<b>Bills:</b>");
        sb.AppendLine();
        int i = billsPage.TotalCount - pageNumber * (countPerPage ?? 0);
        billsPage.Items.ForEach(q =>
        {
            string ledgerLine = string.Empty;
            if (q.LedgerRecords?.Any() ?? false)
            {
                var record = q.LedgerRecords.FirstOrDefault(q => q.CreditorUser.Id == userId || q.DebtorUser.Id == userId);
                if (record is null)
                {
                    ledgerLine = "\n   (created by you)";
                }
                else
                {
                    bool isCreditor = record.CreditorUser.Id == userId;
                    ledgerLine = $"\n   owe {(isCreditor ? '-' : '+')}{record.Amount:0.##} {record.CurrencyCode} to {(isCreditor ? record.DebtorUser : record.CreditorUser)}";
                }
            }
            sb.AppendLine($"<b>{i}.</b> {q.Date} ({q.Status}){ledgerLine}\n{q.Description}\n");
            buttons.Add(new(i.ToString(), ShowBillCommand.FormatCallbackData(q.Id)));
            i--;
        });

        await _publishEndpoint.Publish(new SendTelegramMessage(chatId, sb.ToString(), [buttons, billsPage.ToInlineKeyboardButtons(CommandString)], MessageId: messageId));

        return null;
    }

}
