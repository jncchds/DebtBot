using DebtBot.Models;

namespace DebtBot.Interfaces.Services;

public interface IBudgetService
{
    PagingResult<SpendingModel> GetSpendings(Guid userId, int pageNumber = 0, int? countPerPage = null);
}