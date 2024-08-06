using DebtBot.DB;
using DebtBot.DB.Entities;
using DebtBot.Messages;
using DebtBot.Messages.Notification;
using DebtBot.Models.Enums;
using DebtBot.Telegram.Commands;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using System.Text;

namespace DebtBot.Consumers.Notification;

public class BillCancellationFailedConsumer : IConsumer<BillCancellationFailed>
{
    private readonly DebtContext _debtContext;
    private readonly IPublishEndpoint _publishEndpoint;

    public BillCancellationFailedConsumer(DebtContext debtContext,
        IPublishEndpoint publishEndpoint)
    {
        _debtContext = debtContext;
        _publishEndpoint = publishEndpoint;
    }

    public async Task Consume(ConsumeContext<BillCancellationFailed> context)
    {
        var billMessage = context.Message;

        var bill = await _debtContext
            .Bills
            .Include(q => q.Creator)
            .FirstOrDefaultAsync(q => q.Id == billMessage.BillId, context.CancellationToken);

        if (bill is null)
        {
            throw new Exception("Bill not found");
        }

        sendNotification(bill, bill.Creator);
    }

    private void sendNotification(Bill bill, User user)
    {
        if (!user.TelegramBotEnabled)
            return;

        var sb = new StringBuilder();

        sb.AppendLine("Bill cancellation failed");
        sb.AppendLine($"{bill.Description}");
        sb.AppendLine();

        var replyKeyboard = new List<InlineButtonRecord>()
        {
            new("Show bill", ShowBillCommand.FormatCallbackData(bill.Id)),
            //new("Cancel bill", CancelBillCommand.FormatCallbackData(bill.Id)),
        };

        replyKeyboard.Add(new("Cancel bill", CancelBillCommand.FormatCallbackData(bill.Id)));

        if (bill.Status == ProcessingState.Draft)
        {
            replyKeyboard.Add(new("Finalize bill", FinalizeBillCommand.FormatCallbackData(bill.Id)));
        }

        var telegramMessage = new TelegramMessageRequested(
            user.TelegramId!.Value,
            sb.ToString(),
            InlineKeyboard: [ replyKeyboard ]
            );

        _publishEndpoint.Publish(telegramMessage);
    }
}
