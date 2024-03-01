using AutoMapper;
using DebtBot.DB;
using DebtBot.DB.Entities;
using DebtBot.Extensions;
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

	public PagingResult<SpendingModel> GetSpendings(Guid userId, int pageNumber = 0, int? countPerPage = null)
	{
		var spendings = _debtContext
			.Spendings
			.Where(s => s.UserId == userId)
			.ToPagingResult(pageNumber, countPerPage);

		return new PagingResult<SpendingModel>(spendings.CountPerPage, spendings.PageNumber, spendings.TotalCount, _mapper.Map<List<SpendingModel>>(spendings.Items));
	}
}