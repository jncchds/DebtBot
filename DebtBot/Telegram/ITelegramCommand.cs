using Telegram.Bot;
using Telegram.Bot.Types;

namespace DebtBot.Telegram;

public interface ITelegramCommand
{
    string CommandName { get; }
    Task ExecuteAsync(Message message, ITelegramBotClient botClient, CancellationToken cancellationToken);
}
