using DebtBot.Interfaces.Services;
using DebtBot.Interfaces.Telegram;
using DebtBot.Models.Enums;
using Microsoft.IdentityModel.Tokens;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace DebtBot.Telegram.Commands;

public class ShowBillCommand : ITelegramCommand, ITelegramCallbackQuery
{
    public const string CommandString = "/ShowBill";
    public string CommandName => CommandString;
    
    private readonly IBillService _billService;

    public ShowBillCommand(IBillService billService)
    {
        _billService = billService;
    }

    public async Task ExecuteAsync(CallbackQuery query, ITelegramBotClient botClient, CancellationToken cancellationToken)
    {
        var guidString = query.Data!.Split(" ").Skip(1).FirstOrDefault();
        if (!Guid.TryParse(guidString, out var billId))
        {
            await botClient.AnswerCallbackQueryAsync(query.Id, "bill guid not detected", cancellationToken: cancellationToken);
            return;
        }

        await ShowBill(billId, query.Message!.Chat.Id, botClient, cancellationToken);
        await botClient.AnswerCallbackQueryAsync(query.Id, null, cancellationToken: cancellationToken);
    }

    public async Task ExecuteAsync(ProcessedMessage processedMessage, ITelegramBotClient botClient, CancellationToken cancellationToken)
    {
        var billId = Guid.TryParse(processedMessage.ProcessedText, out var result) ? result : (Guid?)null;
        if (billId is null)
        {
            await botClient.SendTextMessageAsync(processedMessage.ChatId, "Failed to parse bill id", cancellationToken: cancellationToken);
            return;
        }

        await ShowBill(billId.Value, processedMessage.ChatId, botClient, cancellationToken);
    }

    private async Task ShowBill(
        Guid billId, 
        long chatId,
        ITelegramBotClient botClient,
        CancellationToken cancellationToken)
    {
        var bill = _billService.Get(billId);

        if (bill is null)
        {
            await botClient.SendTextMessageAsync(chatId, "Invalid bill id", cancellationToken: cancellationToken);
            return;
        }
        
        var markupList = new List<InlineKeyboardButton>();
        if (bill!.Status == ProcessingState.Draft)
        {
            markupList.Add(InlineKeyboardButton.WithCallbackData(
                "Finalize", 
                $"{FinalizeBillCommand.CommandString} {billId}"));
        }

        var markup = markupList.IsNullOrEmpty() ? null : new InlineKeyboardMarkup(markupList);
        
        await botClient.SendTextMessageAsync(
            chatId, 
            bill!.ToString(),
            replyMarkup: markup,
            cancellationToken: cancellationToken,
            parseMode: ParseMode.Html);
    }
}
