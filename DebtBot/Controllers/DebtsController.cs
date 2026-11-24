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
    private readonly IUserContactService _contacts;

    public DebtsController(IDebtService debtService, IUserContactService contacts)
    {
        _debtService = debtService;
        _contacts = contacts;
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
    /// <c>ofUserId</c>: someone you have a confirmed subscription to (else 403); their balances, with each person under
    /// their own display name.
    /// </summary>
    [HttpGet("Own")]
    public IActionResult GetOwn([FromQuery] DebtListQuery query)
    {
        var owner = ListOwner(_contacts, query.OfUserId, out var denied);
        if (denied is not null)
        {
            return denied;
        }

        query.CurrencyCode = string.IsNullOrWhiteSpace(query.CurrencyCode) ? null : query.CurrencyCode.Trim().ToUpperInvariant();

        return Ok(_debtService.GetOwn(owner, query, ownNames: owner != UserId));
    }
}
