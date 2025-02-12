using DebtBot.Identity;
using DebtBot.Interfaces.Services;
using DebtBot.Models;
using DebtBot.Models.Debt;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DebtBot.Controllers;

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

    [Authorize]
    [HttpGet("Own")]
    public ActionResult<PagingResult<DebtModel>> GetOwn(int pageNumber = 0, int? countPerPage = null)
    {
        var debts = _debtService.GetForUser(UserId!.Value, pageNumber, countPerPage).Items;
        
        return Ok(debts);
    }
}
