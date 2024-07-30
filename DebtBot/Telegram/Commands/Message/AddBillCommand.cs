using DebtBot.Interfaces.Services;
using DebtBot.Interfaces.Telegram;
using DebtBot.Messages;
using DebtBot.Messages.Notification;
using DebtBot.Models.User;
using DebtBot.Services;
using MassTransit;
using Telegram.Bot;

namespace DebtBot.Telegram.Commands.Message;

public class AddBillCommand : ITelegramCommand
{
    private readonly ITelegramService _telegramService;
    private readonly IBillService _billService;
    private readonly IPublishEndpoint _publishEndpoint;

    public AddBillCommand(
        ITelegramService telegramParserService,
        IBillService billService,
        IPublishEndpoint publishEndpoint)
    {
        _telegramService = telegramParserService;
        _billService = billService;
        _publishEndpoint = publishEndpoint;
    }
    public const string CommandString = "/AddBill";
    public string CommandName => CommandString;

    public async Task ExecuteAsync(
        ProcessedMessage processedMessage, 
        ITelegramBotClient botClient,
        CancellationToken cancellationToken)
    {
        var parsedBill = _telegramService.ParseBill(processedMessage.ProcessedText, processedMessage.UserSearchModels);

        if (!parsedBill.IsValid)
        {
            await _publishEndpoint.Publish(new TelegramMessageRequested(
                processedMessage.ChatId,
                string.Join("\n", parsedBill.Errors)));
            return;
        }

        var billId = _billService.Add(parsedBill.Result!, new UserSearchModel() { TelegramId = processedMessage.FromId });

        await _publishEndpoint.Publish(new BillNotificationRequested() { BillId = billId, ChatId = processedMessage.ChatId });
    }
}
