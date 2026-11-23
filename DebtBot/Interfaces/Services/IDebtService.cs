using DebtBot.Models;
using DebtBot.Models.Debt;

namespace DebtBot.Interfaces.Services;
public interface IDebtService
{
    PagingResult<DebtModel> GetForUser(Guid userId, int pageNumber = 0, int? countPerPage = null);
    /// <summary>Only the user's own balances (no subscriptions), without settled ones, filtered and sorted.</summary>
    PagingResult<DebtModel> GetOwn(Guid userId, DebtListQuery filter);
    PagingResult<DebtModel> GetAll(int pageNumber = 0, int? countPerPage= null);
}