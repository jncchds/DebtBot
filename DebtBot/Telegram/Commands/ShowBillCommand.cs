using DebtBot.Interfaces.Services;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;

namespace DebtBot.Telegram.Commands;

public class ShowBillCommand : ITelegramCommand
{
    private readonly IBillService _billService;

    public ShowBillCommand(IBillService billService)
    {
        _billService = billService;
    }

    public string CommandName => "/ShowBill";

    public async Task ExecuteAsync(ProcessedMessage processedMessage, ITelegramBotClient botClient, CancellationToken cancellationToken)
    {
        var billId = Guid.TryParse(processedMessage.ProcessedText, out var result) ? result : (Guid?)null;
        if (billId is null)
        {
            await botClient.SendTextMessageAsync(processedMessage.ChatId, "Failed to parse bill id", cancellationToken: cancellationToken);
            return;
        }

        var bill = _billService.Get(billId.Value);

        if (bill is null)
        {
            await botClient.SendTextMessageAsync(processedMessage.ChatId, "Invalid bill id", cancellationToken: cancellationToken);
            return;
        }

        await botClient.SendTextMessageAsync(processedMessage.ChatId, bill!.ToString(), cancellationToken: cancellationToken, parseMode: ParseMode.Html);
    }
}
