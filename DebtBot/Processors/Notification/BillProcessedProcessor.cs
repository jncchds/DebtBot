using AutoMapper;
using DebtBot.DB;
using DebtBot.DB.Entities;
using DebtBot.Messages;
using DebtBot.Messages.Notification;
using DebtBot.Models;
using DebtBot.Telegram.Commands;
using MassTransit;
using MassTransit.Transports;
using Microsoft.EntityFrameworkCore;
using Polly;
using System.Text;
using Telegram.Bot;

namespace DebtBot.Processors.Notification;

public class BillProcessedNotificationProcessor : INotificationProcessorBase
{
    private readonly DebtContext _debtContext;
    private readonly IMapper _mapper;
    private readonly IPublishEndpoint _publishEndpoint;

    public BillProcessedNotificationProcessor(DebtContext debtContext,
        IMapper mapper,
        IPublishEndpoint publishEndpoint)
    {
        _debtContext = debtContext;
        _mapper = mapper;
        _publishEndpoint = publishEndpoint;
    }

    public NotificationType NotificationType => NotificationType.BillProcessed;

    public Task Process(SendNotificationBase message)
    {
        var billMessage = (SendBillProcessedNotification)message;

        var bill = _debtContext
            .Bills
            .Include(q => q.BillParticipants)
            .ThenInclude(p => p.User)
            .ThenInclude(u => u.Subscriptions)
            .ThenInclude(s => s.Subscriber)
            .Include(b => b.Spendings)
            .Include(b => b.LedgerRecords)
            .ThenInclude(lr => lr.CreditorUser)
        .Include(b => b.LedgerRecords)
            .ThenInclude(lr => lr.DebtorUser)
            .FirstOrDefault(q => q.Id == billMessage.BillId);

        if (bill is null)
        {
            throw new Exception("Bill not found");
        }

        // send message to send notification
        foreach (var participant in bill.BillParticipants)
        {
            SendNotification(bill, participant, participant.User);

            foreach(var subscription in participant.User.Subscriptions)
            {
                if (!subscription.IsConfirmed)
                    continue;

                SendNotification(bill, participant, subscription.Subscriber);
            }
        }

        return Task.CompletedTask;
    }

    private void AppendDebt(StringBuilder sb, string userDisplayName, string otheruserDisplayName, decimal amount, string currencyCode)
    {
        if (amount > 0)
        {
            sb.AppendLine($"{otheruserDisplayName} owes {userDisplayName} extra {amount:0.##} {currencyCode}");
        }
        else
        {
            sb.AppendLine($"{userDisplayName} owes {otheruserDisplayName} extra {-amount:0.##} {currencyCode}");
        }
    }

    public void SendNotification(Bill bill, BillParticipant participant, User user)
    {
        if (!user.TelegramBotEnabled)
            return;

        var sb = new StringBuilder();

        sb.AppendLine($"{participant.User.DisplayName} participated in Bill");
        sb.AppendLine($"{bill.Description}");
        sb.AppendLine();

        var spending = bill.Spendings.FirstOrDefault(s => s.UserId == participant.UserId);
        if (spending == null)
        {
            sb.AppendLine("You didn't spend anything");
        }
        else
        {
            sb.AppendLine(_mapper.Map<SpendingModel>(spending).ToNotificationString());
        }
        sb.AppendLine();

        foreach (var lr in bill.LedgerRecords.Where(r => r.CreditorUserId == participant.UserId))
        {
            AppendDebt(sb, participant.User.DisplayName, lr.DebtorUser.DisplayName, lr.Amount, lr.CurrencyCode);
        }
        sb.AppendLine();
        foreach (var lr in bill.LedgerRecords.Where(r => r.DebtorUserId == participant.UserId))
        {
            AppendDebt(sb, participant.User.DisplayName, lr.CreditorUser.DisplayName, -lr.Amount, lr.CurrencyCode);
        }

        var telegramMessage = new SendTelegramMessage(
            user.TelegramId!.Value,
            sb.ToString(),
            InlineKeyboard:
                [
                    new()
                    {
                        new("Show bill", ShowBillCommand.FormatCallbackData(bill.Id))
                    }
                ]
            );

        _publishEndpoint.Publish(telegramMessage);
    }
}
