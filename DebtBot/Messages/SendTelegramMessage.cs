using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace DebtBot.Messages;

public record SendTelegramMessage
(
    long ChatId,
    string Text,
    List<List<InlineButtonRecord>>? InlineKeyboard = null,
    List<List<string>>? ReplyKeyboard = null,
    int? ThreadId = null,
    int? MessageId = null,
    int? ReplyToMessageId = null,
    ParseMode ParseMode = ParseMode.Html,
    bool DisableNotification = false
);