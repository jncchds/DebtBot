using DebtBot.Interfaces.Services;
using DebtBot.Interfaces.Telegram;
using DebtBot.Models.Enums;
using Microsoft.IdentityModel.Tokens;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace DebtBot.Telegram.Commands;

public class ShowBillCommand : ITelegramCommand
{
    private readonly IBillService _billService;

    public ShowBillCommand(IBillService billService)
    {
        _billService = billService;
    }

    public string CommandName => "/ShowBill";

    public async Task ExecuteAsync(ProcessedMessage processedMessage, ITelegramBotClient botClient, CancellationToken cancellationToken)
    {
        var billId = Guid.TryParse(processedMessage.ProcessedText, out var result) ? result : (Guid?)null;
        if (billId is null)
        {
            await botClient.SendTextMessageAsync(processedMessage.ChatId, "Failed to parse bill id", cancellationToken: cancellationToken);
            return;
        }

        var bill = _billService.Get(billId.Value);

        if (bill is null)
        {
            await botClient.SendTextMessageAsync(processedMessage.ChatId, "Invalid bill id", cancellationToken: cancellationToken);
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
            processedMessage.ChatId, 
            bill!.ToString(),
            replyMarkup: markup,
            cancellationToken: cancellationToken,
            parseMode: ParseMode.Html);
    }
}
