using DebtBot.Interfaces.Services;
using DebtBot.Interfaces.Telegram;
using DebtBot.Messages;
using DebtBot.Messages.Notification;
using DebtBot.Models.Enums;
using MassTransit;
using Telegram.Bot;
using Telegram.Bot.Types;

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

    public async Task<string?> ExecuteAsync(
        CallbackQuery query, 
        ITelegramBotClient botClient, 
        CancellationToken cancellationToken)
    {
        var guidString = query.Data!.Split(" ").Skip(1).FirstOrDefault();
        Guid billId;
        if (!Guid.TryParse(guidString, out billId))
        {
            return "bill guid not detected";
        }

        await FinalizeAsync(billId, query.Message!.Chat.Id, botClient, cancellationToken);

        return null;
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
                await _publishEndpoint.Publish(new SendTelegramMessage(
                    processedMessage.ChatId, 
                    "Bill id not detected"));
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
            await _publishEndpoint.Publish(new SendBillNotification() { BillId = billId, ChatId = chatId });
        }
        else
        {
            await _publishEndpoint.Publish(new SendTelegramMessage(chatId, "There was an error finalizing the bill"));
        }
    }

    public static string FormatCallbackData(Guid billId)
    {
        return $"{CommandString} {billId}";
    }
}
