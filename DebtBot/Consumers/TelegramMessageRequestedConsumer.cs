using DebtBot.Extensions;
using DebtBot.Messages;
using DebtBot.Telegram;
using MassTransit;
using Telegram.Bot;

namespace DebtBot.Consumers;

public class TelegramMessageRequestedConsumer : IConsumer<TelegramMessageRequested>
{
    private readonly ITelegramBotClient _botClient;
    private readonly TelegramRateLimiter _rateLimiter;

    public TelegramMessageRequestedConsumer(ITelegramBotClient botClient,
        TelegramRateLimiter rateLimiter)
    {
        _botClient = botClient;
        _rateLimiter = rateLimiter;
    }

    public async Task Consume(ConsumeContext<TelegramMessageRequested> context)
    {
        await _rateLimiter.ProcessAsync(() => _botClient.SendOrUpdateTelegramMessage(context.Message, CancellationToken.None));
    }
}
