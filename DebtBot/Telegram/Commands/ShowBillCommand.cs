using DebtBot.Interfaces.Telegram;
using DebtBot.Messages;
using MassTransit;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace DebtBot.Telegram.Commands;

public class ShowBillCommand : ITelegramCommand, ITelegramCallbackQuery
{
    public const string CommandString = "/ShowBill";
    public string CommandName => CommandString;
    
    private readonly IPublishEndpoint _publishEndpoint;

    public ShowBillCommand(IPublishEndpoint publishEndpoint)
    {
        _publishEndpoint = publishEndpoint;
    }

    public async Task ExecuteAsync(CallbackQuery query, ITelegramBotClient botClient, CancellationToken cancellationToken)
    {
        var guidString = query.Data!.Split(" ").Skip(1).FirstOrDefault();
        if (!Guid.TryParse(guidString, out var billId))
        {
            await botClient.AnswerCallbackQueryAsync(query.Id, "bill guid not detected", cancellationToken: cancellationToken);
            return;
        }

        await _publishEndpoint.Publish(new SendBillMessage(billId, query.Message!.Chat.Id));

        await botClient.AnswerCallbackQueryAsync(query.Id, null, cancellationToken: cancellationToken);
    }

    public async Task ExecuteAsync(ProcessedMessage processedMessage, ITelegramBotClient botClient, CancellationToken cancellationToken)
    {
        var billId = Guid.TryParse(processedMessage.ProcessedText, out var result) ? result : (Guid?)null;
        if (billId is null)
        {
            await botClient.SendTextMessageAsync(processedMessage.ChatId, "Failed to parse bill id", cancellationToken: cancellationToken);
            return;
        }

        await _publishEndpoint.Publish(new SendBillMessage(billId.Value, processedMessage.ChatId));
    }

    public static string FormatCallbackData(Guid billId)
    {
        return $"{CommandString} {billId}";
    }
}
