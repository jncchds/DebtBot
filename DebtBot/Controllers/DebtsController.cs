using DebtBot.Interfaces.Services;
using Microsoft.AspNetCore.Mvc;

namespace DebtBot.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class DebtsController : ControllerBase
{
    private readonly IDebtService _debtService;

    public DebtsController(IDebtService debtService)
    {
        _debtService = debtService;
    }

    [HttpGet()]
    public IActionResult Get()
    {
        return Ok(_debtService.GetAll());
    }

    [HttpGet("{id}")]
    public IActionResult Get(Guid id)
    {
        var debts = _debtService.Get(id);
        
        return Ok(debts);
    }
}
