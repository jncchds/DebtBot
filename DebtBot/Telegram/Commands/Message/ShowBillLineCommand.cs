﻿using System.Text;
using DebtBot.Interfaces.Services;
using DebtBot.Interfaces.Telegram;
using DebtBot.Messages;
using MassTransit;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;

namespace DebtBot.Telegram.Commands.Message;

public class ShowBillLineCommand : ITelegramCommand
{
    private readonly IBillService _billService;
    private readonly IPublishEndpoint _publishEndpoint;

    public ShowBillLineCommand(IBillService billService,
        IPublishEndpoint publishEndpoint)
    {
        _billService = billService;
        _publishEndpoint = publishEndpoint;
    }

    public const string CommandString = "/ShowBillLine";
    public string CommandName => CommandString;


    public async Task ExecuteAsync(ProcessedMessage processedMessage, ITelegramBotClient botClient, CancellationToken cancellationToken)
    {
        var billLineId = Guid.TryParse(processedMessage.ProcessedText, out var result) ? result : (Guid?)null;
        if (billLineId is null)
        {
            await _publishEndpoint.Publish(new SendTelegramMessage(processedMessage.ChatId, "Failed to parse bill line id"));
            return;
        }

        var billLine = _billService.GetLine(billLineId.Value);

        if (billLine is null)
        {
            await _publishEndpoint.Publish(new SendTelegramMessage(processedMessage.ChatId, "Invalid bill line id"));
            return;
        }

        var bill = _billService.Get(billLine.BillId);

        StringBuilder sb = new StringBuilder();
        sb.AppendLine($"<span class=\"tg-spoiler\">BillLine {billLine.Id}</span>");
        billLine.AppendToStringBuilder(sb, bill!.CurrencyCode);

        await _publishEndpoint.Publish(new SendTelegramMessage(processedMessage.ChatId, sb.ToString()));
    }
}
