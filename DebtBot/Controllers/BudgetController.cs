using DebtBot.Interfaces.Services;
using DebtBot.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DebtBot.Controllers;

[Authorize]
[ApiController]
[Route("api/v1/spendings")]
public class BudgetController : DebtBotControllerBase
{
    private readonly IBudgetService _budgetService;

    public BudgetController(IBudgetService budgetService)
    {
        _budgetService = budgetService;
    }

    [HttpGet("Own")]
    public IActionResult GetOwn(int pageNumber = 0, int? countPerPage = null)
    {
        var spendings = _budgetService.GetSpendings(UserId!.Value, pageNumber, countPerPage);
        return Ok(spendings);
    }
}
