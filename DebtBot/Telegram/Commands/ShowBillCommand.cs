﻿using DebtBot.Interfaces.Telegram;
using DebtBot.Messages;
using DebtBot.Messages.Notification;
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

    public async Task<string?> ExecuteAsync(CallbackQuery query, CancellationToken cancellationToken)
    {
        var guidString = query.Data!.Split(" ").Skip(1).FirstOrDefault();
        if (!Guid.TryParse(guidString, out var billId))
        {
            return "bill guid not detected";
        }

        await _publishEndpoint.Publish(new BillNotificationRequested() { BillId = billId, ChatId = query.Message!.Chat.Id });

        return null;
    }

    public async Task ExecuteAsync(ProcessedMessage processedMessage, ITelegramBotClient botClient, CancellationToken cancellationToken)
    {
        Guid? billId = null;
        if (processedMessage.ObjectType == Models.Enums.ObjectType.Bill)
            billId = processedMessage.ObjectId!.Value;
        else
            billId = Guid.TryParse(processedMessage.ProcessedText, out var result) ? result : (Guid?)null;

        if (billId is null)
        {
            await _publishEndpoint.Publish(new TelegramMessageRequested(processedMessage.ChatId, "Failed to parse bill id"));
            return;
        }

        await _publishEndpoint.Publish(new BillNotificationRequested() { BillId = billId.Value, ChatId = processedMessage.ChatId });
    }

    public static string FormatCallbackData(Guid billId)
    {
        return $"{CommandString} {billId}";
    }
}
