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
    private readonly IUserContactService _contacts;

    public SpendingsController(IBudgetService budgetService, IUserContactService contacts)
    {
        _budgetService = budgetService;
        _contacts = contacts;
    }

    /// <summary>
    /// My spendings (my share of finalized bills). By default newest first, without cancelled bills' spendings.
    /// Query: <c>currencyCode</c> (bill currency, any case), <c>dateFrom</c>, <c>dateTo</c> (whole days, UTC),
    /// <c>includeCancelled</c>, <c>sortBy</c> (Date, Amount), <c>sortOrder</c> (Asc, Desc), <c>pageNumber</c>, <c>countPerPage</c>.
    /// <c>ofUserId</c>: someone you have a confirmed subscription to (else 403); their spendings.
    /// </summary>
    [HttpGet("Own")]
    public IActionResult GetOwn([FromQuery] SpendingListQuery query)
    {
        var owner = ListOwner(_contacts, query.OfUserId, out var denied);
        if (denied is not null)
        {
            return denied;
        }

        query.CurrencyCode = string.IsNullOrWhiteSpace(query.CurrencyCode) ? null : query.CurrencyCode.Trim().ToUpperInvariant();

        return Ok(_budgetService.GetSpendings(owner, query));
    }

    /// <summary>
    /// My totals per bill currency over non-cancelled spendings, ordered by currency: <c>[{ currencyCode, amount, count }]</c>.
    /// <c>ofUserId</c> as on <c>Own</c>.
    /// </summary>
    [HttpGet("Own/summary")]
    public IActionResult GetOwnSummary(DateTime? dateFrom = null, DateTime? dateTo = null, Guid? ofUserId = null)
    {
        var owner = ListOwner(_contacts, ofUserId, out var denied);
        if (denied is not null)
        {
            return denied;
        }

        return Ok(_budgetService.GetSummary(owner, dateFrom, dateTo));
    }
}
