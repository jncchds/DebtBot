using DebtBot.Interfaces.Telegram;
using Telegram.Bot.Types;

namespace DebtBot.Telegram.Commands.CallbackQueries;

public class IgnoreCallbackQuery : ITelegramCallbackQuery
{
    public const string CommandString = "/Ignore";
    public string CommandName => CommandString;

    public async Task<string?> ExecuteAsync(CallbackQuery query, CancellationToken cancellationToken)
    {
        return null;
    }
}
