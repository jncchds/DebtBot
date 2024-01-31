using DebtBot.Identity;
using DebtBot.Interfaces.Services;
using DebtBot.Models.Bill;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DebtBot.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class BillsController : ControllerBase
{
	private readonly IBillService _billService;

	public BillsController(IBillService billService)
	{
		_billService = billService;
	}

	[HttpGet("{id}")]
	public IActionResult Get(Guid id)
	{
		var bill = _billService.Get(id);
		if (bill is null)
		{
			return NotFound();
		}

		return Ok(bill);
	}

    [Authorize(IdentityData.AdminUserPolicyName)]
    [HttpGet]
	public IActionResult Get()
	{
		return Ok(_billService.Get());
	}

	[HttpPost]
	public IActionResult Post(BillCreationModel billModel)
	{
		return Ok(_billService.AddBill(billModel));
	}

	[HttpPost("{id}/finalize")]
	public IActionResult Finalize(Guid id)
	{
		var result = _billService.Finalize(id);
        if (!result)
        {
            return BadRequest();
        }
        return Ok();
    }
	
}