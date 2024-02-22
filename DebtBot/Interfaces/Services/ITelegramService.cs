using DebtBot.Models.Bill;
using DebtBot.Models.User;
using Telegram.Bot.Types;

namespace DebtBot.Services;
public interface ITelegramService
{
    Guid? GetUserByTelegramId(long telegramId);
    (string processedText, List<UserSearchModel> entities) IncludeMentions(string? message, List<MessageEntity> entities);
    void Actualize(User user);
    public BillParserModel ParseBill(string? message, List<MessageEntity> entities);
}