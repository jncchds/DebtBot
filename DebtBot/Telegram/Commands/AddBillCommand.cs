using DebtBot.Interfaces.Services;
using DebtBot.Models.User;
using DebtBot.Services;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;

namespace DebtBot.Telegram.Commands;

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
    public string CommandName => "/AddBill";

    public async Task ExecuteAsync(
        ProcessedMessage processedMessage, 
        ITelegramBotClient botClient,
        CancellationToken cancellationToken)
    {
        var parsedBill = _telegramService.ParseBill(processedMessage.ProcessedText, processedMessage.UserSearchModels);
        var billId = _billService.AddBill(parsedBill, new UserSearchModel() { TelegramId = processedMessage.FromId });
        await botClient.SendTextMessageAsync(
            processedMessage.ChatId, 
            $"Bill added with id ```{billId}```", 
            cancellationToken: cancellationToken,
            parseMode: ParseMode.MarkdownV2);
    }
}
