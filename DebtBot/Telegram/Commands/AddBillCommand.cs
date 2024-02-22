using DebtBot.Interfaces.Services;
using DebtBot.Services;
using DebtBot.Models.User;
using Telegram.Bot;
using Telegram.Bot.Types;
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

    public async Task ExecuteAsync(Message message, ITelegramBotClient botClient, CancellationToken cancellationToken)
    {
        var creatorId = _telegramService.GetUserByTelegramId(message.From!.Id);
        if (creatorId is null)
            return;
        var parsedBill = _telegramService.ParseBill(message.Text, message.Entities?.ToList() ?? []);
        var billId = _billService.AddBill(parsedBill, new UserSearchModel() { Id = creatorId });
        await botClient.SendTextMessageAsync(message.Chat.Id, $"Bill added with id ```{billId}```", 
            cancellationToken: cancellationToken,
            parseMode: ParseMode.MarkdownV2);
    }
}
