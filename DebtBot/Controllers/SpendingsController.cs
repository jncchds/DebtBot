using DebtBot.Interfaces.Services;
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

    /// <summary>My spendings (my share of finalized bills), newest first. Cancelled bills' spendings have <c>isCanceled</c>.</summary>
    [HttpGet("Own")]
    public IActionResult GetOwn(int pageNumber = 0, int? countPerPage = null)
    {
        return Ok(_budgetService.GetSpendings(UserId!.Value, pageNumber, countPerPage));
    }
}
