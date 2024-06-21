using DebtBot.Interfaces.Repositories;
using DebtBot.Interfaces.Services;
using DebtBot.Models;

namespace DebtBot.Services;

public class BudgetService : IBudgetService
{
	private readonly IBudgetRepository _budgetRepository;

	public BudgetService(IBudgetRepository budgetRepository)
	{
		_budgetRepository = budgetRepository;
	}

	public PagingResult<SpendingModel> GetSpendings(Guid userId, int pageNumber = 0, int? countPerPage = null)
	{
		return _budgetRepository.GetSpendings(userId, pageNumber, countPerPage);
	}
}