using DebtBot.Models.User;
using Telegram.Bot.Types;

namespace DebtBot.Services;
public interface ITelegramService
{
    Guid? GetUserByTelegramId(long telegramId);
    (string processedText, List<UserModel> entities) IncludeMentions(Guid creatorId, string? message, MessageEntity[]? entities);
    void Actualize(User user);
}