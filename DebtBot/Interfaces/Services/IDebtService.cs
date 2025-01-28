using DebtBot.Models;
using DebtBot.Models.Debt;

namespace DebtBot.Interfaces.Services;
public interface IDebtService
{
    PagingResult<DebtModel> GetForUser(Guid userId, int pageNumber = 0, int? countPerPage = null);
    PagingResult<DebtModel> GetAll(int pageNumber = 0, int? countPerPage= null);
}