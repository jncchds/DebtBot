using DebtBot.Models;

namespace DebtBot.Interfaces.Services;

public interface IBillService
{
    BillModel? Get(Guid id);
    List<BillModel> Get();
    void AddBill(BillModel billModel);
}