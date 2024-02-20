using DebtBot.Interfaces.Services;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace DebtBot.Telegram.Commands;

public class FinilizeBillCommand : ITelegramCommand
{
    private readonly IBillService _billService;

    public FinilizeBillCommand(IBillService billService)
    {
        _billService = billService;
    }

    public string CommandName => "/FinilizeBill";

    public async Task ExecuteAsync(Message message, ITelegramBotClient botClient, CancellationToken cancellationToken)
    {
        var text = message.Text.Substring(CommandName.Length).Trim();
        var guid = Guid.TryParse(text, out var result) ? result : (Guid?)null;
        if (guid is null)
        {
            await botClient.SendTextMessageAsync(message.Chat.Id, "Invalid bill id", cancellationToken: cancellationToken);
            return;
        }
        var ok = _billService.Finalize(result);
        if (ok)
        {
            await botClient.SendTextMessageAsync(message.Chat.Id, "Bill finalized", cancellationToken: cancellationToken);
        }
        else
        {
            await botClient.SendTextMessageAsync(message.Chat.Id, "There was an error finilizing the bill", cancellationToken: cancellationToken);
        }
    }
}
