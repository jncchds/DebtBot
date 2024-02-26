using System.Text;
using DebtBot.DB;
using DebtBot.Messages;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Telegram.Bot;

namespace DebtBot.Processors;

public class NotifyBillProcessedConsumer : IConsumer<NotifyBillProcessed>
{
	private readonly DebtContext _debtContext;
	private readonly ITelegramBotClient _telegramBotClient;

	public NotifyBillProcessedConsumer(
		DebtContext debtContext, 
		ITelegramBotClient telegramBotClient)
	{
		_debtContext = debtContext;
		_telegramBotClient = telegramBotClient;
	}

	public Task Consume(ConsumeContext<NotifyBillProcessed> context)
	{
		var bill = _debtContext
			.Bills
			.Include(q => q.BillParticipants)
			.ThenInclude(p => p.User)
			.Include(b => b.Spendings)
			.Include(b => b.LedgerRecords)
			.ThenInclude(lr => lr.CreditorUser)
			.Include(b => b.LedgerRecords)
			.ThenInclude(lr => lr.DebtorUser)
			.FirstOrDefault(q => q.Id == context.Message.billId);
		
		if (bill is null)
		{
			throw new Exception("Bill not found");
		}
		
		// send message to send notification
		foreach (var participant in bill.BillParticipants.Where(p => p.User.TelegramBotEnabled))
		{
			var sb = new StringBuilder();
			sb.AppendLine("You participated in Bill");
			sb.AppendLine($"{bill.Description}");
			sb.AppendLine();
			sb.AppendLine($"You spent {bill.Spendings.FirstOrDefault(s => s.UserId == participant.UserId)?.Amount ?? 0:0.##} {bill.CurrencyCode}" );
			foreach (var lr in bill.LedgerRecords.Where(r => r.CreditorUserId == participant.UserId))
			{
				sb.AppendLine($"{lr.DebtorUser.DisplayName} owes you {lr.Amount} {lr.CurrencyCode}");
			}
			sb.AppendLine();
			foreach (var lr in bill.LedgerRecords.Where(r => r.DebtorUserId == participant.UserId))
			{
				sb.AppendLine($"you owe {lr.Amount} {lr.CurrencyCode} to {lr.CreditorUser.DisplayName}");
			}
			sb.AppendLine();
			
			_telegramBotClient.SendTextMessageAsync(participant.User.TelegramId!, sb.ToString());
		}

		return Task.CompletedTask;
	}
}