using DebtBot.Models.Bill;
using DebtBot.Models.User;

namespace DebtBot.Services;
public interface IParserService
{
    BillCreationModel ParseBill(Guid creatorId, string billString);
    BillCreationModel ParseBill(Guid creatorId, string billString, List<UserModel> mentions);
}