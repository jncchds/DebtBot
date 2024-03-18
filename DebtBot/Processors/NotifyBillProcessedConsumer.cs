using AutoMapper;
using DebtBot.DB;
using DebtBot.Messages;
using DebtBot.Models;
using DebtBot.Telegram.Commands;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using System.Text;
using Telegram.Bot;
using Telegram.Bot.Types.ReplyMarkups;

namespace DebtBot.Processors;

public class NotifyBillProcessedConsumer : IConsumer<NotifyBillProcessed>
{
	private readonly DebtContext _debtContext;
	private readonly ITelegramBotClient _telegramBotClient;
    private readonly IMapper _mapper;
    private readonly IPublishEndpoint _publishEndpoint;

    public NotifyBillProcessedConsumer(
		DebtContext debtContext, 
		ITelegramBotClient telegramBotClient,
		IMapper mapper,
		IPublishEndpoint publishEndpoint)
	{
		_debtContext = debtContext;
		_telegramBotClient = telegramBotClient;
        _mapper = mapper;
		_publishEndpoint = publishEndpoint;
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

			var message = new SendTelegramMessage(
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


            _publishEndpoint.Publish(message);
		}

		return Task.CompletedTask;
	}

	private void AppendDebt(StringBuilder sb, string otheruserDisplayName, decimal amount, string currencyCode)
	{
        if (amount>0)
        {
			sb.AppendLine($"{otheruserDisplayName} owes you extra {amount:0.##} {currencyCode}");
        }
		else
		{
			sb.AppendLine($"You owe {otheruserDisplayName} extra {-amount:0.##} {currencyCode}");
		}
    }
}