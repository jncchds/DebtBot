using DebtBot.Messages;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace DebtBot.Extensions;

public static class TelegramBotExtensions
{
    public static async Task SendOrUpdateTelegramMessage(this ITelegramBotClient botClient, TelegramMessageRequested message, CancellationToken cancellationToken)
    {
        var inlineButtons = message.InlineKeyboard?.Select(
            row => row.Select(
                button => InlineKeyboardButton.WithCallbackData(button.text, button.data)
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
