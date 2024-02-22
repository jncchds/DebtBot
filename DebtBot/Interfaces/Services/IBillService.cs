using DebtBot.Models.Bill;
using DebtBot.Models.User;

namespace DebtBot.Interfaces.Services;

public interface IBillService
{
    BillModel? Get(Guid id);
    List<BillModel> Get();
    Guid AddBill(BillCreationModel billModel, Guid creatorId);
    Guid AddBill(string billString, Guid creatorId);
    Guid AddBill(BillParserModel parsedBill, UserSearchModel creator);
    bool Finalize(Guid id);
    List<BillModel> GetByUser(Guid userId);
    bool HasAccess(Guid userId, BillModel? bill);
}