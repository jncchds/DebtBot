using DebtBot.Interfaces.Telegram;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace DebtBot.Telegram.Commands.CallbackQueries;

public class IgnoreCallbackQuery : ITelegramCallbackQuery
{
    public const string CommandString = "/Ignore";
    public string CommandName => CommandString;

    public async Task<string?> ExecuteAsync(CallbackQuery query, ITelegramBotClient botClient, CancellationToken cancellationToken)
    {
        return null;
    }
}
