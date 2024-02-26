using DebtBot.DB;
using DebtBot.Messages;
using MassTransit;
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
		var participants = _debtContext
			.BillParticipants
			.Where(p =>
				p.BillId == context.Message.billId
				&& p.User.TelegramBotEnabled)
			.Select(q => q.User);

		// send message to send notification
		foreach (var participant in participants)
		{
			_telegramBotClient.SendTextMessageAsync(participant.TelegramId!, "You owe new money");
		}

		return Task.CompletedTask;
	}
}