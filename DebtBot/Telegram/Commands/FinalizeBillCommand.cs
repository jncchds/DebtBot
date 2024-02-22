using DebtBot.Interfaces.Services;
using Telegram.Bot;

namespace DebtBot.Telegram.Commands;

public class FinalizeBillCommand : ITelegramCommand
{
    private readonly IBillService _billService;

    public FinalizeBillCommand(IBillService billService)
    {
        _billService = billService;
    }

    public string CommandName => "/FinalizeBill";

    public async Task ExecuteAsync(
        ProcessedMessage processedMessage, 
        ITelegramBotClient botClient,
        CancellationToken cancellationToken)
    {
        var guid = Guid.TryParse(processedMessage.ProcessedText, out var result) ? result : (Guid?)null;
        if (guid is null)
        {
            await botClient.SendTextMessageAsync(processedMessage.ChatId, "Invalid bill id", cancellationToken: cancellationToken);
            return;
        }
        var ok = _billService.Finalize(result);
        if (ok)
        {
            await botClient.SendTextMessageAsync(processedMessage.ChatId, "Bill finalized", cancellationToken: cancellationToken);
        }
        else
        {
            await botClient.SendTextMessageAsync(processedMessage.ChatId, "There was an error finalizing the bill", cancellationToken: cancellationToken);
        }
    }
}
