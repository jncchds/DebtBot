using AutoMapper;
using DebtBot.DB;
using DebtBot.DB.Entities;
using DebtBot.Extensions;
using DebtBot.Interfaces.Services;
using DebtBot.Models;
using DebtBot.Models.Enums;
using Microsoft.EntityFrameworkCore;

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
			.Include(s => s.User)
			.OrderByDescending(s => s.Date)
			.Where(s => s.UserId == userId)
			.ToPagingResult(pageNumber, countPerPage);

		return new PagingResult<SpendingModel>(spendings.CountPerPage, spendings.PageNumber, spendings.TotalCount, _mapper.Map<List<SpendingModel>>(spendings.Items));
	}

	public PagingResult<SpendingModel> GetSpendings(Guid userId, SpendingListQuery filter)
	{
		var query = Filter(userId, filter.CurrencyCode, filter.DateFrom, filter.DateTo, filter.IncludeCancelled);

		var desc = filter.SortOrder == SortOrder.Desc;
		// Cast for SQLite (tests), which can't order by decimal; Postgres orders the same.
		var sorted = filter.SortBy switch
		{
			SpendingSortField.Amount => (desc ? query.OrderByDescending(s => (double)s.Amount) : query.OrderBy(s => (double)s.Amount))
				.ThenByDescending(s => s.Date),
			_ => desc ? query.OrderByDescending(s => s.Date) : query.OrderBy(s => s.Date),
		};

		// The bill id is unique per user, so pages are stable.
		var spendings = sorted
			.ThenBy(s => s.BillId)
			.Include(s => s.User)
			.ToPagingResult(filter.PageNumber, filter.CountPerPage);

		return new PagingResult<SpendingModel>(spendings.CountPerPage, spendings.PageNumber, spendings.TotalCount, _mapper.Map<List<SpendingModel>>(spendings.Items));
	}

	public List<SpendingSummaryModel> GetSummary(Guid userId, DateTime? dateFrom, DateTime? dateTo)
	{
		// Summed in memory: decimals stay exact, and SQLite (tests) can't sum decimals.
		return Filter(userId, null, dateFrom, dateTo, false)
			.Select(s => new { s.CurrencyCode, s.Amount })
			.AsEnumerable()
			.GroupBy(s => s.CurrencyCode)
			.Select(g => new SpendingSummaryModel { CurrencyCode = g.Key, Amount = g.Sum(s => s.Amount), Count = g.Count() })
			.OrderBy(s => s.CurrencyCode, StringComparer.Ordinal)
			.ToList();
	}

	private IQueryable<Spending> Filter(Guid userId, string? currencyCode, DateTime? dateFrom, DateTime? dateTo, bool includeCancelled)
	{
		var query = _debtContext.Spendings.Where(s => s.UserId == userId);

		if (!includeCancelled)
		{
			query = query.Where(s => !s.IsCanceled);
		}

		if (currencyCode != null)
		{
			query = query.Where(s => s.CurrencyCode == currencyCode);
		}

		// Whole days in UTC; a date from the query string has no kind, which Npgsql won't send as timestamptz.
		if (dateFrom != null)
		{
			var from = DateTime.SpecifyKind(dateFrom.Value.Date, DateTimeKind.Utc);
			query = query.Where(s => s.Date >= from);
		}

		if (dateTo != null)
		{
			var before = DateTime.SpecifyKind(dateTo.Value.Date.AddDays(1), DateTimeKind.Utc);
			query = query.Where(s => s.Date < before);
		}

		return query;
	}
}