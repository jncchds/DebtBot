using DebtBot.Models;
using DebtBot.Models.Bill;

namespace DebtBot.Services;
public interface IParserService
{
    ValidationModel<BillParserModel> ParseBill(Guid creatorId, string billString);
}