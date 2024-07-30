using Telegram.Bot.Types;

namespace DebtBot.Interfaces.Telegram;

public interface ITelegramCallbackQuery
{
    Task<string?> ExecuteAsync(CallbackQuery query, CancellationToken cancellationToken);
    string CommandName { get; }
}
