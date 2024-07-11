using DebtBot.Models;
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
    ValidationModel<BillParserModel> ParseBill(string parsedText, List<UserSearchModel> userSearchModels);
    ValidationModel<List<BillLineParserModel>> ParseLines(string parsedText, List<UserSearchModel> userSearchModels);
    ValidationModel<List<BillPaymentParserModel>> ParsePayments(string parsedText, List<UserSearchModel> userSearchModels);
    ValidationModel<(BillParserModel forwardBill, BillParserModel backwardBill)> ParseExchange(string parsedText, List<UserSearchModel> userSearchModels);
}