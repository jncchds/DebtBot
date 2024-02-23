using DebtBot.Telegram;
using Telegram.Bot;

namespace DebtBot.Interfaces.Telegram;

public interface ITelegramCommand
{
    string CommandName { get; }
    Task ExecuteAsync(ProcessedMessage processedMessage, ITelegramBotClient botClient, CancellationToken cancellationToken);
}
