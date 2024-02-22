using Telegram.Bot;

namespace DebtBot.Telegram;

public interface ITelegramCommand
{
    string CommandName { get; }
    Task ExecuteAsync(ProcessedMessage processedMessage, ITelegramBotClient botClient, CancellationToken cancellationToken);
}
