using DebtBot.Models;

namespace DebtBot.Interfaces.Services;
public interface IDebtService
{
    List<DebtModel> Get(Guid id);
    List<DebtModel> GetAll();
}