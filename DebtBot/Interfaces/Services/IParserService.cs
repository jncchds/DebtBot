using DebtBot.Models.Bill;

namespace DebtBot.Services;
public interface IParserService
{
    BillCreationModel ParseBill(Guid creatorId, string billString);
}