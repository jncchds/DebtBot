using DebtBot.Models;
using DebtBot.Models.Debt;

namespace DebtBot.Interfaces.Services;
public interface IDebtService
{
    PagingResult<DebtModel> GetForUser(Guid userId, int pageNumber = 0, int? countPerPage = null);
    /// <summary>Only the user's own balances (no subscriptions), without settled ones, filtered and sorted.</summary>
    /// <param name="ownNames">Name each person by their own display name instead of the user's name for them (someone else's list).</param>
    PagingResult<DebtModel> GetOwn(Guid userId, DebtListQuery filter, bool ownNames = false);
    PagingResult<DebtModel> GetAll(int pageNumber = 0, int? countPerPage= null);
}