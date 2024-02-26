using DebtBot.Models;

namespace DebtBot.Interfaces.Services;

public interface IBudgetService
{
	List<SpendingModel> GetSpendings(Guid userId);
}