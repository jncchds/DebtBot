using DebtBot.Identity;
using DebtBot.Interfaces.Services;
using DebtBot.Models.Debt;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DebtBot.Controllers;

[Authorize]
[ApiController]
[Route("api/v1/[controller]")]
public class DebtsController : DebtBotControllerBase
{
    private readonly IDebtService _debtService;

    public DebtsController(IDebtService debtService)
    {
        _debtService = debtService;
    }

    [Authorize(IdentityData.AdminUserPolicyName)]
    [HttpGet()]
    public IActionResult Get(int pageNumber = 0, int? countPerPage = null)
    {
        return Ok(_debtService.GetAll(pageNumber, countPerPage));
    }

    [Authorize(IdentityData.AdminUserPolicyName)]
    [HttpGet("{id}")]
    public IActionResult Get(Guid id, int pageNumber = 0, int? countPerPage = null)
    {
        var debts = _debtService.GetForUser(id, pageNumber, countPerPage);
        
        return Ok(debts);
    }

    /// <summary>
    /// My own balances, without settled ones. By default by person, then currency.
    /// Query: <c>withUserId</c>, <c>currencyCode</c> (any case), <c>direction</c> (All, OwedToMe, IOwe),
    /// <c>sortBy</c> (Person, Amount, Currency), <c>sortOrder</c> (Asc, Desc), <c>pageNumber</c>, <c>countPerPage</c>.
    /// </summary>
    [HttpGet("Own")]
    public IActionResult GetOwn([FromQuery] DebtListQuery query)
    {
        query.CurrencyCode = string.IsNullOrWhiteSpace(query.CurrencyCode) ? null : query.CurrencyCode.Trim().ToUpperInvariant();

        return Ok(_debtService.GetOwn(UserId!.Value, query));
    }
}
