using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using Telegram.Bot;
using DebtBot.Messages;

namespace DebtBot.Extensions;

public static class TelegramBotExtensions
{
    public static async Task SendOrUpdateTelegramMessage(this ITelegramBotClient botClient, SendTelegramMessage message, CancellationToken cancellationToken)
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
            await botClient.EditMessageTextAsync(
                message.ChatId,
                message.MessageId.Value,
                message.Text,
                message.ParseMode,
                replyMarkup: inlineMarkup,
                cancellationToken: cancellationToken);
        }
        else
        {
            await botClient.SendTextMessageAsync(
                message.ChatId,
                message.Text,
                message.ThreadId,
                message.ParseMode,
                replyToMessageId: message.ReplyToMessageId,
                replyMarkup: (IReplyMarkup?)inlineMarkup ?? replyMarkup,
                cancellationToken: cancellationToken);
        }
    }

    public static List<List<string>> DefaultReplyKeyboard =>
        new([[ "/Debts", "ShowBills", "/Spendings" ]]);
}
