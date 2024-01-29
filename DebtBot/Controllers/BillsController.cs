using DebtBot.Models;
using DebtBot.Services;
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
	
	[HttpGet]
	public IActionResult Get()
	{
		return Ok(_billService.Get());
	}

	[HttpPost]
	public IActionResult Post(BillModel billModel)
	{
		_billService.AddBill(billModel);

		return Ok();
	}
	
}