using DebtBot.Interfaces.Services;
using DebtBot.Messages;
using DebtBot.Messages.Notification;
using DebtBot.Telegram.Commands.CallbackQueries;
using MassTransit;

namespace DebtBot.Consumers.Notification;

public class ExchangeCreatedConsumer : IConsumer<ExchangeCreated>
{
    private readonly IBillService _billService;
    private readonly IPublishEndpoint _publishEndpoint;

    public ExchangeCreatedConsumer(IBillService billService, IPublishEndpoint publishEndpoint)
    {
        _billService = billService;
        _publishEndpoint = publishEndpoint;
    }

    public async Task Consume(ConsumeContext<ExchangeCreated> context)
    {
        var exchangeData = context.Message;

        var markup = new List<InlineButtonRecord>();

        markup.Add(new("Finalize", FinalizeExchangeCommand.FormatCallbackData(exchangeData.ForwardBillId, exchangeData.BackwardBillId)));

        await _publishEndpoint.Publish(new TelegramMessageRequested(
            exchangeData.ChatId,
            "<b>Exchange created</b>",
            InlineKeyboard: [markup]),
            context.CancellationToken);

        return;
    }
}