using DebtBot.Messages;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace DebtBot.Extensions;

public static class TelegramBotExtensions
{
    public static async Task SendOrUpdateTelegramMessage(this ITelegramBotClient botClient, TelegramMessageRequested message, CancellationToken cancellationToken)
    {
        try
        {
            await SendOrUpdate(botClient, message, cancellationToken);
        }
        catch (ApiRequestException e) when (e.ErrorCode == 400 && message.InlineKeyboard?.Any(row => row.Any(b => b.url is not null)) == true)
        {
            // Telegram refuses some link buttons (localhost, a bare host name…): send the message without them.
            var rows = message.InlineKeyboard!
                .Select(row => row.Where(b => b.url is null).ToList())
                .Where(row => row.Count > 0)
                .ToList();
            await SendOrUpdate(botClient, message with { InlineKeyboard = rows.Count > 0 ? rows : null }, cancellationToken);
        }
    }

    private static async Task SendOrUpdate(ITelegramBotClient botClient, TelegramMessageRequested message, CancellationToken cancellationToken)
    {
        var inlineButtons = message.InlineKeyboard?.Select(
            row => row.Select(
                button => button.url is not null
                    ? InlineKeyboardButton.WithUrl(button.text, button.url)
                    : InlineKeyboardButton.WithCallbackData(button.text, button.data)
            ).ToList()
        ).ToList();

        var inlineMarkup = inlineButtons != null ? new InlineKeyboardMarkup(inlineButtons) : null;

        var replyButtons = message.ReplyKeyboard?.Select(
            row => row.Select(
                button => new KeyboardButton(button)
            ).ToList()
        ).ToList();

        var replyMarkup = replyButtons != null ? new ReplyKeyboardMarkup(replyButtons)
        {
            ResizeKeyboard = true
        } : null;

        if (message.MessageId.HasValue)
        {
            await botClient.EditMessageText(
                message.ChatId,
                message.MessageId.Value,
                message.Text,
                parseMode: message.ParseMode,
                replyMarkup: inlineMarkup,
                cancellationToken: cancellationToken);
        }
        else
        {
            await botClient.SendMessage(
                message.ChatId,
                message.Text,
                parseMode: message.ParseMode,
                replyParameters: message.ReplyToMessageId is int replyToMessageId
                    ? new ReplyParameters { MessageId = replyToMessageId }
                    : null,
                replyMarkup: (ReplyMarkup?)inlineMarkup ?? replyMarkup,
                messageThreadId: message.ThreadId,
                cancellationToken: cancellationToken);
        }
    }

    public static List<List<string>> DefaultReplyKeyboard =>
        new([[ "/Debts", "/Bills", "/Spendings" ]]);
}
