using DebtBot.Models;

namespace DebtBot.Interfaces.Services;

public interface IBudgetService
{
    /// <summary>The bot's list: every spending, cancelled ones too, newest first.</summary>
    PagingResult<SpendingModel> GetSpendings(Guid userId, int pageNumber = 0, int? countPerPage = null);
    /// <summary>The web list: filtered and sorted; cancelled spendings only when asked for.</summary>
    PagingResult<SpendingModel> GetSpendings(Guid userId, SpendingListQuery filter);
    /// <summary>Totals of non-cancelled spendings per bill currency, ordered by currency.</summary>
    List<SpendingSummaryModel> GetSummary(Guid userId, DateTime? dateFrom, DateTime? dateTo);
}