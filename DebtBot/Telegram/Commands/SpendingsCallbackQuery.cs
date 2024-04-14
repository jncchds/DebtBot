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

public class SpendingsCallbackQuery : ITelegramCallbackQuery, ITelegramCommand
{
    private readonly ITelegramService _telegramService;
    private readonly IBudgetService _budgetService;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly TelegramConfiguration _telegramConfig;

    public SpendingsCallbackQuery(
        ITelegramService telegramService,
        IBudgetService budgetService,
        IOptions<DebtBotConfiguration> debtBotConfig,
        IPublishEndpoint publishEndpoint)
    {
        _telegramService = telegramService;
        _budgetService = budgetService;
        _publishEndpoint = publishEndpoint;
        _telegramConfig = debtBotConfig.Value.Telegram;
    }

    public const string CommandString = "/Spendings";
    public string CommandName => CommandString;

    public async Task<string?> ExecuteAsync(
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

        await ShowSpendings(query.From.Id, query.Message!.Chat.Id, botClient, pageNumber, countPerPage, messageId, cancellationToken);

        return null;
    }

    public async Task ExecuteAsync(ProcessedMessage processedMessage, ITelegramBotClient botClient, CancellationToken cancellationToken)
    {
        await ShowSpendings(processedMessage.FromId, processedMessage.ChatId, botClient, 0, _telegramConfig.CountPerPage, null, cancellationToken);
    }

    private async Task ShowSpendings(long telegramId, long chatId, ITelegramBotClient botClient,
        int pageNumber, int? countPerPage, int? messageId, CancellationToken cancellationToken)
    {
        var user = _telegramService.GetUserByTelegramId(telegramId);

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
            buttons.Add(InlineKeyboardButton.WithCallbackData(i.ToString(), ShowBillCommand.FormatCallbackData(spending.BillId)));
        }

        await _publishEndpoint.Publish(            
            new SendTelegramMessage(
                chatId,
                sb.ToString(),
                InlineKeyboard: [spendingsPage.ToInlineKeyboardButtons(CommandString)]
                ));
    }
}