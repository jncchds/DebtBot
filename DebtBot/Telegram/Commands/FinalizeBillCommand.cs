using DebtBot.Interfaces.Services;
using DebtBot.Interfaces.Telegram;
using DebtBot.Messages;
using DebtBot.Models.Enums;
using MassTransit;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace DebtBot.Telegram.Commands;

public class FinalizeBillCommand : ITelegramCommand, ITelegramCallbackQuery
{
    public const string CommandString = "/FinalizeBill";
    public string CommandName => CommandString;

    private readonly IBillService _billService;
    private readonly IPublishEndpoint _publishEndpoint;

    public FinalizeBillCommand(IBillService billService, IPublishEndpoint publishEndpoint)
    {
        _billService = billService;
        _publishEndpoint = publishEndpoint;
    }

    public async Task ExecuteAsync(
        CallbackQuery query, 
        ITelegramBotClient botClient, 
        CancellationToken cancellationToken)
    {
        var guidString = query.Data!.Split(" ").Skip(1).FirstOrDefault();
        Guid billId;
        if (!Guid.TryParse(guidString, out billId))
        {
            await botClient.AnswerCallbackQueryAsync(query.Id, "bill guid not detected", cancellationToken: cancellationToken);
            return;
        }

        await FinalizeAsync(billId, query.Message!.Chat.Id, botClient, cancellationToken);
        await botClient.AnswerCallbackQueryAsync(query.Id, null, cancellationToken: cancellationToken);
    }

    public async Task ExecuteAsync(
        ProcessedMessage processedMessage, 
        ITelegramBotClient botClient,
        CancellationToken cancellationToken)
    {
        var trimmed = processedMessage.ProcessedText.Trim();
        Guid billId;

        if (processedMessage.ObjectType == ObjectType.Bill && processedMessage.ObjectId != null)
        {
            billId = processedMessage.ObjectId.Value;
        }
        else
        {
            if (!Guid.TryParse(trimmed, out billId))
            {
                await botClient.SendTextMessageAsync(processedMessage.ChatId, $"Bill id not detected",
                    cancellationToken: cancellationToken,
                    parseMode: ParseMode.MarkdownV2);
                return;
            }
        }
        
        await FinalizeAsync(billId, processedMessage.ChatId, botClient, cancellationToken);
    }

    private async Task FinalizeAsync(
        Guid billId, 
        long chatId, 
        ITelegramBotClient botClient,
        CancellationToken cancellationToken)
    {
        var ok = _billService.Finalize(billId);
        if (ok)
        {
            await _publishEndpoint.Publish(new SendBillMessage(billId, chatId));
        }
        else
        {
            await botClient.SendTextMessageAsync(chatId, "There was an error finalizing the bill", cancellationToken: cancellationToken);
        }
    }

    public static string FormatCallbackData(Guid billId)
    {
        return $"{CommandString} {billId}";
    }
}
