using DebtBot.Interfaces.Services;
using DebtBot.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DebtBot.Controllers;

[Authorize]
[ApiController]
[Route("api/v1/[controller]")]
public class SpendingsController : DebtBotControllerBase
{
    private readonly IBudgetService _budgetService;

    public SpendingsController(IBudgetService budgetService)
    {
        _budgetService = budgetService;
    }

    /// <summary>
    /// My spendings (my share of finalized bills). By default newest first, without cancelled bills' spendings.
    /// Query: <c>currencyCode</c> (bill currency, any case), <c>dateFrom</c>, <c>dateTo</c> (whole days, UTC),
    /// <c>includeCancelled</c>, <c>sortBy</c> (Date, Amount), <c>sortOrder</c> (Asc, Desc), <c>pageNumber</c>, <c>countPerPage</c>.
    /// </summary>
    [HttpGet("Own")]
    public IActionResult GetOwn([FromQuery] SpendingListQuery query)
    {
        query.CurrencyCode = string.IsNullOrWhiteSpace(query.CurrencyCode) ? null : query.CurrencyCode.Trim().ToUpperInvariant();

        return Ok(_budgetService.GetSpendings(UserId!.Value, query));
    }

    /// <summary>My totals per bill currency over non-cancelled spendings, ordered by currency: <c>[{ currencyCode, amount, count }]</c>.</summary>
    [HttpGet("Own/summary")]
    public IActionResult GetOwnSummary(DateTime? dateFrom = null, DateTime? dateTo = null)
    {
        return Ok(_budgetService.GetSummary(UserId!.Value, dateFrom, dateTo));
    }
}
