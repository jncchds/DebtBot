using DebtBot.Interfaces.Services;
using DebtBot.Interfaces.Telegram;
using DebtBot.Models.Enums;
using DebtBot.Models.User;
using DebtBot.Services;
using Microsoft.IdentityModel.Tokens;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace DebtBot.Telegram.Commands.Message;

public class AddBillCommand : ITelegramCommand
{
    private readonly ITelegramService _telegramService;
    private readonly IBillService _billService;

    public AddBillCommand(
        ITelegramService telegramParserService,
        IBillService billService)
    {
        _telegramService = telegramParserService;
        _billService = billService;
    }
    public const string CommandString = "/AddBill";
    public string CommandName => CommandString;

    public async Task ExecuteAsync(
        ProcessedMessage processedMessage, 
        ITelegramBotClient botClient,
        CancellationToken cancellationToken)
    {
        var parsedBill = _telegramService.ParseBill(processedMessage.ProcessedText, processedMessage.UserSearchModels);
        var billId = _billService.Add(parsedBill, new UserSearchModel() { TelegramId = processedMessage.FromId });
        
        var bill = _billService.Get(billId);
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
