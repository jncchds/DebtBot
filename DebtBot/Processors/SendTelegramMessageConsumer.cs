using DebtBot.Extensions;
using DebtBot.Messages;
using MassTransit;
using Telegram.Bot;

namespace DebtBot.Processors;

public class SendTelegramMessageConsumer : IConsumer<SendTelegramMessage>
{
    private readonly ITelegramBotClient _botClient;
    private readonly RateLimittingProcessor _processor;

    public SendTelegramMessageConsumer(ITelegramBotClient botClient,
        RateLimittingProcessor processor)
    {
        _botClient = botClient;
        _processor = processor;
    }

    public async Task Consume(ConsumeContext<SendTelegramMessage> context)
    {
        await _processor.ProcessAsync(() => _botClient.SendOrUpdateTelegramMessage(context.Message, CancellationToken.None));
    }
}
