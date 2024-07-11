using DebtBot.Interfaces.Services;
using DebtBot.Interfaces.Telegram;
using DebtBot.Messages;
using DebtBot.Messages.Notification;
using DebtBot.Models.User;
using DebtBot.Services;
using MassTransit;
using Telegram.Bot;

namespace DebtBot.Telegram.Commands.Message;

public class ExchangeDebtCommand : ITelegramCommand
{
    public const string CommandString = "/ExchangeDebt";

    private readonly ITelegramService _telegramService;
    private readonly IBillService _billService;
    private readonly IPublishEndpoint _publishEndpoint;

    public ExchangeDebtCommand(ITelegramService telegramService, IBillService billService, IPublishEndpoint publishEndpoint)
    {
        _telegramService = telegramService;
        _billService = billService;
        _publishEndpoint = publishEndpoint;
    }

    public string CommandName => CommandString;

    public async Task ExecuteAsync(ProcessedMessage processedMessage, ITelegramBotClient botClient, CancellationToken cancellationToken)
    {
        var parsedExchange = _telegramService.ParseExchange(processedMessage.ProcessedText, processedMessage.UserSearchModels);

        if (!parsedExchange.IsValid)
        {
            await _publishEndpoint.Publish(new SendTelegramMessage(
                processedMessage.ChatId,
                string.Join("\n", parsedExchange.Errors)));
            return;
        }

        var billId = _billService.Add(parsedExchange.Result!.forwardBill!, new UserSearchModel() { TelegramId = processedMessage.FromId });

        await _publishEndpoint.Publish(new SendBillNotification() { BillId = billId, ChatId = processedMessage.ChatId });

        billId = _billService.Add(parsedExchange.Result!.backwardBill!, new UserSearchModel() { TelegramId = processedMessage.FromId });

        await _publishEndpoint.Publish(new SendBillNotification() { BillId = billId, ChatId = processedMessage.ChatId });
    }
}
