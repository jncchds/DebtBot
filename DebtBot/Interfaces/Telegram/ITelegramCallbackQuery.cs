using Telegram.Bot.Types;
using Telegram.Bot;

namespace DebtBot.Interfaces.Telegram;

public interface ITelegramCallbackQuery
{
    Task ExecuteAsync(CallbackQuery query, ITelegramBotClient botClient, CancellationToken cancellationToken);
    string CommandName { get; }
}
