using DebtBot.Interfaces.Services;
using DebtBot.Services;
using System.Text;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace DebtBot.Telegram.Commands;

public class AddBillCommand : ITelegramCommand
{
    private readonly ITelegramService _telegramService;
    private readonly IParserService _parserService;
    private readonly IBillService _billService;

    public AddBillCommand(ITelegramService telegramParserService,
        IParserService parserService,
        IBillService billService)
    {
        _telegramService = telegramParserService;
        _parserService = parserService;
        _billService = billService;
    }
    public string CommandName => "/AddBill";

    public async Task ExecuteAsync(Message message, ITelegramBotClient botClient, CancellationToken cancellationToken)
    {
        var creatorId = _telegramService.GetUserByTelegramId(message.From!.Id);
        if (creatorId is null)
            return;
        (var convertedText, var mentions) = _telegramService.IncludeMentions(creatorId.Value, message.Text, message.Entities);
        var bill = _parserService.ParseBill(creatorId.Value, convertedText, mentions);
        var billId = _billService.AddBill(bill, creatorId.Value);
        await botClient.SendTextMessageAsync(message.Chat.Id, $"Bill added with id ```{billId}```", cancellationToken: cancellationToken);
    }
}
