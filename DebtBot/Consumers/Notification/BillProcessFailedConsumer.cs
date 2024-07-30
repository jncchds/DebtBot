using DebtBot.DB;
using DebtBot.DB.Entities;
using DebtBot.Messages;
using DebtBot.Messages.Notification;
using DebtBot.Telegram.Commands;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using System.Text;

namespace DebtBot.Consumers.Notification;

public class BillProcessFailedConsumer : IConsumer<BillProcessFailed>
{
    private readonly DebtContext _debtContext;
    private readonly IPublishEndpoint _publishEndpoint;

    public BillProcessFailedConsumer(DebtContext debtContext,
        IPublishEndpoint publishEndpoint)
    {
        _debtContext = debtContext;
        _publishEndpoint = publishEndpoint;
    }

    public async Task Consume(ConsumeContext<BillProcessFailed> context)
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

        sb.AppendLine("Bill failed to be processed and was reverted to Draft state");
        sb.AppendLine($"{bill.Description}");
        sb.AppendLine();

        var telegramMessage = new TelegramMessageRequested(
            user.TelegramId!.Value,
            sb.ToString(),
            InlineKeyboard:
                [
                    new()
                    {
                        new("Show bill", ShowBillCommand.FormatCallbackData(bill.Id)),
                        new("Finalize bill", FinalizeBillCommand.FormatCallbackData(bill.Id))
                    }
                ]
            );

        _publishEndpoint.Publish(telegramMessage);
    }
}
