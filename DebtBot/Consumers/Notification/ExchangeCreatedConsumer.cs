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

        var forwardBill = await _billService.GetAsync(exchangeData.ForwardBillId, context.CancellationToken);
        var backwardBill = await _billService.GetAsync(exchangeData.BackwardBillId, context.CancellationToken);

        var forwardPayment = forwardBill?.Payments.FirstOrDefault();
        var backwardPayment = backwardBill?.Payments.FirstOrDefault();

        if (forwardPayment is null || backwardPayment is null)
        {
            await _publishEndpoint.Publish(new TelegramMessageRequested(
                exchangeData.ChatId,
                "Error creating exchange"),
                context.CancellationToken);

            return;
        }

        var text = $"Exchange created:\n"
            + $"{forwardPayment.User} will owe {backwardPayment.User} extra {backwardPayment.Amount:0.##} {backwardBill!.PaymentCurrencyCode}\n"
            + $"{backwardPayment.User} will owe {forwardPayment.User} extra {forwardPayment.Amount:0.##} {forwardBill!.PaymentCurrencyCode}";

        var markup = new List<InlineButtonRecord>();

        markup.Add(new("Finalize", FinalizeExchangeCommand.FormatCallbackData(exchangeData.ForwardBillId, exchangeData.BackwardBillId)));

        await _publishEndpoint.Publish(new TelegramMessageRequested(
            exchangeData.ChatId,
            text,
            InlineKeyboard: [markup]),
            context.CancellationToken);

        return;
    }
}