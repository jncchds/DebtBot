using AutoMapper;
using DebtBot.DB;
using DebtBot.Interfaces.Services;
using DebtBot.Models;

namespace DebtBot.Services;

public class BudgetService : IBudgetService
{
	private readonly DebtContext _debtContext;
	private readonly IMapper _mapper;

	public BudgetService(DebtContext debtContext, IMapper mapper)
	{
		_debtContext = debtContext;
		_mapper = mapper;
	}

	public List<SpendingModel> GetSpendings(Guid userId)
	{
		var spendings = _debtContext
			.Spendings
			.Where(s => s.UserId == userId)
			.ToList();

		return _mapper.Map<List<SpendingModel>>(spendings);
	}
}