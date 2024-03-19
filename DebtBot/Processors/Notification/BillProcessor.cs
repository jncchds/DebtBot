using DebtBot.Interfaces.Services;
using DebtBot.Messages;
using DebtBot.Messages.Notification;
using DebtBot.Models.Enums;
using DebtBot.Telegram.Commands;
using MassTransit;
using Polly;

namespace DebtBot.Processors.Notification;

public class BillProcessor : INotificationProcessorBase
{
    private readonly IBillService _billService;
    private readonly IPublishEndpoint _publishEndpoint;

    public BillProcessor(IBillService billService, IPublishEndpoint publishEndpoint)
    {
        _billService = billService;
        _publishEndpoint = publishEndpoint;
    }

    public NotificationType NotificationType => NotificationType.Bill;

    public async Task Process(SendNotificationBase message)
    {
        var showBillMessage = (SendBillNotification)message;

        var bill = _billService.Get(showBillMessage.BillId);

        if (bill is null)
        {
            await _publishEndpoint.Publish(new SendTelegramMessage(
                showBillMessage.ChatId,
                "Invalid bill id"));
            return;
        }

        var markup = new List<InlineButtonRecord>();
        if (bill!.Status == ProcessingState.Draft)
        {
            markup.Add(new("Finalize", FinalizeBillCommand.FormatCallbackData(showBillMessage.BillId)));
        }

        await _publishEndpoint.Publish(new SendTelegramMessage(
            showBillMessage.ChatId,
            bill!.ToString(),
            InlineKeyboard: [markup]));

        return;
    }
}