using DebtBot.Interfaces.Services;
using DebtBot.Messages;
using DebtBot.Messages.Notification;
using DebtBot.Telegram.Commands.CallbackQueries;
using MassTransit;

namespace DebtBot.Processors.Notification;

public class SendExchangeNotificationConsumer : INotificationProcessorBase
{
    private readonly IBillService _billService;
    private readonly IPublishEndpoint _publishEndpoint;

    public SendExchangeNotificationConsumer(IBillService billService, IPublishEndpoint publishEndpoint)
    {
        _billService = billService;
        _publishEndpoint = publishEndpoint;
    }

    public NotificationType NotificationType => NotificationType.Exchange;

    public async Task Process(SendNotificationBase message)
    {
        var exchangeData = (SendExchangeNotification)message;

        var markup = new List<InlineButtonRecord>();

        markup.Add(new("Finalize", FinalizeExchangeCommand.FormatCallbackData(exchangeData.ForwardBillId, exchangeData.BackwardBillId)));

        await _publishEndpoint.Publish(new SendTelegramMessage(
            exchangeData.ChatId,
            "<b>Exchange created</b>",
            InlineKeyboard: [markup]));

        return;
    }
}