using DebtBot.Models.Bill;

namespace DebtBot.Interfaces.Services;

public interface IBillService
{
    BillModel? Get(Guid id);
    List<BillModel> Get();
    Guid AddBill(BillCreationModel billModel);
    Guid AddBill(Guid userId, string billString);
    bool Finalize(Guid id);
    List<BillModel> GetByUser(Guid userId);
    bool HasAccess(Guid userId, BillModel? bill);
}