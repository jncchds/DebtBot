using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using Telegram.Bot;

namespace DebtBot.Extensions;

public static class TelegramBotExtensions
{
    public static async Task SendOrUpdateTelegramMessage(this ITelegramBotClient botClient, long chatId, int? messageId, string text, List<List<InlineKeyboardButton>> buttons, CancellationToken cancellationToken)
    {
        if (messageId.HasValue)
        {
            await botClient.EditMessageTextAsync(
                chatId,
                messageId.Value,
                text,
                parseMode: ParseMode.Html,
                replyMarkup: new InlineKeyboardMarkup(buttons),
                cancellationToken: cancellationToken);
        }
        else
        {
            await botClient.SendTextMessageAsync(
                chatId,
                text,
                parseMode: ParseMode.Html,
                replyMarkup: new InlineKeyboardMarkup(buttons),
                cancellationToken: cancellationToken);
        }
    }

    public static ReplyKeyboardMarkup DefaultReplyKeyboard =>
        new(new List<KeyboardButton>
        {
            new KeyboardButton("/Debts"),
            new KeyboardButton("/ShowBills"),
            new KeyboardButton("/Spendings")
        })
        {
            ResizeKeyboard = true
        };
}
