using DebtBot.Models.Bill;
using DebtBot.Models.User;
using DebtBot.Telegram;
using Telegram.Bot.Types;

namespace DebtBot.Services;

public interface ITelegramService
{
    Guid? GetUserByTelegramId(long telegramId);
    ProcessedMessage? ProcessMessage(Message message, long? botId);
    void Actualize(User user);
    BillParserModel ParseBill(string parsedText, List<UserSearchModel> userSearchModels);
    List<BillLineParserModel> ParseLines(string parsedText, List<UserSearchModel> userSearchModels);
    List<BillPaymentParserModel> ParsePayments(string parsedText, List<UserSearchModel> userSearchModels);
}