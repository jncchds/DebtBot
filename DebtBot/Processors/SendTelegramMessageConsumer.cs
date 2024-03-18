using DebtBot.Extensions;
using DebtBot.Messages;
using MassTransit;
using Telegram.Bot;

namespace DebtBot.Processors;

public class SendTelegramMessageConsumer : IConsumer<SendTelegramMessage>
{
    private readonly ITelegramBotClient _botClient;

    public SendTelegramMessageConsumer(ITelegramBotClient botClient)
    {
        _botClient = botClient;
    }

    public async Task Consume(ConsumeContext<SendTelegramMessage> context)
    {
        await _botClient.SendOrUpdateTelegramMessage(context.Message, CancellationToken.None);
    }
}
