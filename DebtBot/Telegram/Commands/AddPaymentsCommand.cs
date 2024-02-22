﻿using DebtBot.Interfaces.Services;
using DebtBot.Models.User;
using DebtBot.Services;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;

namespace DebtBot.Telegram.Commands;

public class AddPaymentsCommand : ITelegramCommand
{
    private readonly ITelegramService _telegramService;
    private readonly IBillService _billService;

    public AddPaymentsCommand(
        ITelegramService telegramParserService,
        IBillService billService)
    {
        _telegramService = telegramParserService;
        _billService = billService;
    }

    public string CommandName => "/AddPayments";

    public async Task ExecuteAsync(ProcessedMessage processedMessage, ITelegramBotClient botClient, CancellationToken cancellationToken)
    {
        var trimmed = processedMessage.ProcessedText.Trim();
        var index = trimmed.IndexOf("\n", StringComparison.InvariantCulture);
        var guidString = trimmed.Substring(0, index);
        var parsedText = trimmed.Substring(index + 1);

        if (!Guid.TryParse(guidString, out var billId))
        {
            await botClient.SendTextMessageAsync(processedMessage.ChatId, $"Bill id not detected",
                 cancellationToken: cancellationToken,
                 parseMode: ParseMode.MarkdownV2);
            return;
        }

        var payments = _telegramService.ParsePayments(parsedText, processedMessage.UserSearchModels);
        _billService.AddPayments(billId, payments, new UserSearchModel { TelegramId = processedMessage.FromId });
        await botClient.SendTextMessageAsync(
            processedMessage.ChatId,
            $"Payments added to bill with id ```{billId}```",
            cancellationToken: cancellationToken,
            parseMode: ParseMode.MarkdownV2);
    }
}