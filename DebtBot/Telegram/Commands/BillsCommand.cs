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
    private readonly IUserService _userService;

    public BillsCommand(
        IBillService billService,
        IUserService userService,
        ITelegramService telegramService,
        IOptions<DebtBotConfiguration> debtBotConfig,
        IPublishEndpoint publishEndpoint)
    {
        _billService = billService;
        _telegramService = telegramService;
        _telegramConfig = debtBotConfig.Value.Telegram;
        _publishEndpoint = publishEndpoint;
        _userService = userService;
    }

    public async Task<string?> ExecuteAsync(
        CallbackQuery query,
        CancellationToken cancellationToken)
    {
        int pageNumber = 0;
        int countPerPage = _telegramConfig.CountPerPage;
        int? messageId = null;
        Guid? filterByUserId = null;
        bool openInNew = false;
        string? filterByCurrencyCode = null;
        var parametrs = query.Data!.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parametrs.Length > 1)
        {
            _ = int.TryParse(parametrs[1], out pageNumber);
            messageId = query.Message!.MessageId;
        }

        if (parametrs.Length > 2)
        {
            openInNew = bool.Parse(parametrs[2]);
        }

        if (parametrs.Length>3)
        {
            filterByUserId = Guid.Parse(parametrs[3]);
        }
        if (parametrs.Length>4)
        {
            filterByCurrencyCode = parametrs[4];
        }

        var telegramId = query.From.Id;
        var chatId = query.Message!.Chat.Id;

        var ret = await ShowBills(telegramId,
                                  filterByUserId,
                                  filterByCurrencyCode,
                                  chatId,
                                  pageNumber,
                                  countPerPage,
                                  openInNew ? null : messageId,
                                  cancellationToken);

        return ret;
    }

    public async Task ExecuteAsync(ProcessedMessage processedMessage, ITelegramBotClient botClient, CancellationToken cancellationToken)
    {
        var ret = await ShowBills(
            processedMessage.FromId,
            null,
            null,
            processedMessage.ChatId,
            0,
            _telegramConfig.CountPerPage,
            null,
            cancellationToken);

        if (!string.IsNullOrWhiteSpace(ret))
        {
            await _publishEndpoint.Publish(new TelegramMessageRequested(processedMessage.ChatId, ret));
        }
    }

    private async Task<string?> ShowBills(
        long telegramId,
        Guid? filterByUserId,
        string? filterByCurrencyCode,
        long chatId,
        int pageNumber,
        int? countPerPage,
        int? messageId,
        CancellationToken cancellationToken)
    {
        var userId = _telegramService.GetUserByTelegramId(telegramId);
        if (userId is null)
        {
            return "User not detected";
        }

        var billsPage = _billService.GetForUser(userId.Value, filterByUserId, filterByCurrencyCode, pageNumber, countPerPage);
        var filterByUser = _userService.GetUserDisplayModelById(filterByUserId ?? Guid.Empty);

        var buttons = new List<InlineButtonRecord>();

        var sb = new StringBuilder();
        sb.Append("<b>Bills");
        if (filterByUser is not null)
        {
            sb.Append($" related to {filterByUser} ");
        }
        if (!string.IsNullOrWhiteSpace(filterByCurrencyCode))
        {
            sb.Append($" in {filterByCurrencyCode}");
        }
        sb.AppendLine(":</b>");
        sb.AppendLine();
        int billIndex = billsPage.TotalCount - pageNumber * (countPerPage ?? 0);
        billsPage.Items.ForEach(q =>
        {
            sb.AppendLine($"<b>{billIndex}.</b> {q.Date} ({q.Status})");
            if (q.LedgerRecords?.Any() ?? false)
            {
                foreach(var lRecord in q.LedgerRecords)
                {
                    if (lRecord.CreditorUser.Id == userId && (lRecord.DebtorUser.Id == (filterByUserId ?? lRecord.DebtorUser.Id)))
                    {
                        sb.AppendLine($"   owe {-lRecord.Amount:0.##} {lRecord.CurrencyCode} to {lRecord.DebtorUser}");
                    }
                    else if (lRecord.DebtorUser.Id == userId && (lRecord.CreditorUser.Id == (filterByUserId ?? lRecord.CreditorUser.Id)))
                    {
                        sb.AppendLine($"   owe {lRecord.Amount:0.##} {lRecord.CurrencyCode} to {lRecord.CreditorUser}");
                    }
                }
            }
            sb.AppendLine($"{q.Description}\n");
            buttons.Add(new(billIndex.ToString(), ShowBillCommand.FormatCallbackData(q.Id)));
            billIndex--;
        });

        await _publishEndpoint.Publish(
            new TelegramMessageRequested(
                chatId, 
                sb.ToString(),
                [buttons, billsPage.ToInlineKeyboardButtons(CommandString, FormatCallbackParameters(filterByUserId, filterByCurrencyCode))],
                MessageId: messageId));

        return null;
    }

    public static string FormatCallbackData(Guid? userId, string? currencyCode, bool openInNew = false)
    {
        return $"{CommandString} 0 {FormatCallbackParameters(userId, currencyCode, openInNew)}";
    }

    public static string FormatCallbackParameters(Guid? userId, string? currencyCode, bool openInNew = false)
    {
        return $"{openInNew} {userId} {currencyCode}";
    }
}
