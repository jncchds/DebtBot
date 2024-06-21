using DebtBot.Models;

namespace DebtBot.Interfaces.Repositories;
public interface IBudgetRepository
{
    PagingResult<SpendingModel> GetSpendings(Guid userId, int pageNumber = 0, int? countPerPage = null);
}