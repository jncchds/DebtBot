using DebtBot.Interfaces.Services;
using DebtBot.Messages;
using DebtBot.Messages.Notification;
using DebtBot.Models.Enums;
using DebtBot.Telegram.Commands;
using MassTransit;

namespace DebtBot.Consumers.Notification;

public class BillNotificationRequestedConsumer : IConsumer<BillNotificationRequested>
{
    private readonly IBillService _billService;
    private readonly IPublishEndpoint _publishEndpoint;

    public BillNotificationRequestedConsumer(IBillService billService, IPublishEndpoint publishEndpoint)
    {
        _billService = billService;
        _publishEndpoint = publishEndpoint;
    }

    public async Task Consume(ConsumeContext<BillNotificationRequested> context)
    {
        var showBillMessage = context.Message;

        var bill = await _billService.GetAsync(showBillMessage.BillId, context.CancellationToken);

        if (bill is null)
        {
            await _publishEndpoint.Publish(new TelegramMessageRequested(
                showBillMessage.ChatId,
                "Invalid bill id"));
            return;
        }

        var markup = new List<InlineButtonRecord>();
        if (bill!.Status == ProcessingState.Draft)
        {
            markup.Add(new("Finalize", FinalizeBillCommand.FormatCallbackData(showBillMessage.BillId)));
        }

        await _publishEndpoint.Publish(new TelegramMessageRequested(
            showBillMessage.ChatId,
            bill!.ToString(),
            MessageId: showBillMessage.MessageId,
            InlineKeyboard: [markup]));

        return;
    }
}