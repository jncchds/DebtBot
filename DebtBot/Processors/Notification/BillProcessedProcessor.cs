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

    public Task Process(SendNotificationBaseMessage message)
    {
        var billMessage = (SendBillProcessedNotificationMessage)message;

        var bill = _debtContext
            .Bills
            .Include(q => q.BillParticipants)
            .ThenInclude(p => p.User)
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
        foreach (var participant in bill.BillParticipants.Where(p => p.User.TelegramBotEnabled))
        {
            SendNotification(bill, participant);
        }

        return Task.CompletedTask;
    }

    private void AppendDebt(StringBuilder sb, string otheruserDisplayName, decimal amount, string currencyCode)
    {
        if (amount > 0)
        {
            sb.AppendLine($"{otheruserDisplayName} owes you extra {amount:0.##} {currencyCode}");
        }
        else
        {
            sb.AppendLine($"You owe {otheruserDisplayName} extra {-amount:0.##} {currencyCode}");
        }
    }

    public void SendNotification(Bill bill, BillParticipant participant)
    {
        var sb = new StringBuilder();

        sb.AppendLine("You participated in Bill");
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
            AppendDebt(sb, lr.DebtorUser.DisplayName, lr.Amount, lr.CurrencyCode);
        }
        sb.AppendLine();
        foreach (var lr in bill.LedgerRecords.Where(r => r.DebtorUserId == participant.UserId))
        {
            AppendDebt(sb, lr.CreditorUser.DisplayName, -lr.Amount, lr.CurrencyCode);
        }

        var telegramMessage = new SendTelegramMessage(
            participant.User.TelegramId!.Value,
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
