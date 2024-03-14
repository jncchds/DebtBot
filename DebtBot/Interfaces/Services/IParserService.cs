using DebtBot.Models.Bill;

namespace DebtBot.Services;
public interface IParserService
{
    BillParserModel ParseBill(Guid creatorId, string billString);
}