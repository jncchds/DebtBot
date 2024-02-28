using System.Text;
using DebtBot.Interfaces.Services;
using DebtBot.Interfaces.Telegram;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;

namespace DebtBot.Telegram.Commands.Message;

public class ShowBillLineCommand : ITelegramCommand
{
    private readonly IBillService _billService;

    public ShowBillLineCommand(IBillService billService)
    {
        _billService = billService;
    }

    public const string CommandString = "/ShowBillLine";
    public string CommandName => CommandString;


    public async Task ExecuteAsync(ProcessedMessage processedMessage, ITelegramBotClient botClient, CancellationToken cancellationToken)
    {
        var billLineId = Guid.TryParse(processedMessage.ProcessedText, out var result) ? result : (Guid?)null;
        if (billLineId is null)
        {
            await botClient.SendTextMessageAsync(processedMessage.ChatId, "Failed to parse bill line id", cancellationToken: cancellationToken);
            return;
        }

        var billLine = _billService.GetLine(billLineId.Value);

        if (billLine is null)
        {
            await botClient.SendTextMessageAsync(processedMessage.ChatId, "Invalid bill line id", cancellationToken: cancellationToken);
            return;
        }

        var bill = _billService.Get(billLine.BillId);

        StringBuilder sb = new StringBuilder();
        sb.AppendLine($"<span class=\"tg-spoiler\">BillLine {billLine.Id}</span>");
        billLine.AppendToStringBuilder(sb, bill!.CurrencyCode);

        await botClient.SendTextMessageAsync(processedMessage.ChatId, sb.ToString(), cancellationToken: cancellationToken, parseMode: ParseMode.Html);
    }
}
