using DebtBot.Identity;
using DebtBot.Interfaces.Services;
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
    public IActionResult Get()
    {
        return Ok(_debtService.GetAll());
    }

    [Authorize(IdentityData.AdminUserPolicyName)]
    [HttpGet("{id}")]
    public IActionResult Get(Guid id)
    {
        var debts = _debtService.Get(id);
        
        return Ok(debts);
    }
    
    [HttpGet("Own")]
    public IActionResult GetOwn()
    {
        var debts = _debtService.Get(UserId.Value);
        
        return Ok(debts);
    }
}
