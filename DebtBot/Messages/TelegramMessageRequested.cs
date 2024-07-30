using Telegram.Bot.Types.Enums;

namespace DebtBot.Messages;

public record TelegramMessageRequested
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