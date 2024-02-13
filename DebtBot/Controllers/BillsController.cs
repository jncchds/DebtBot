﻿using DebtBot.Crutch;
using DebtBot.Extensions;
using DebtBot.Identity;
using DebtBot.Interfaces.Services;
using DebtBot.Models.Bill;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DebtBot.Controllers;

[Authorize]
[ApiController]
[Route("api/v1/[controller]")]
public class BillsController : DebtBotControllerBase
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

		if (!_billService.HasAccess(UserId!.Value, bill))
		{
			return NotFound();
		}

		return Ok(bill);
    }

    [HttpGet("Own")]
    public IActionResult GetOwn()
    {
        var bills = _billService.GetByUser(UserId!.Value);

        return Ok(bills);
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
		return Ok(_billService.AddBill(billModel, UserId!.Value));
	}

    /// <summary>
    /// An endpoint to parse a bill from a text message
    /// </summary>
    /// <remarks>
    /// Format:
    /// 
    ///		Description (multiple rows, no empty lines)
    ///		
    ///		Total with tips in bill currency
    ///		Currency Code (3 letters)
    ///		Payment currency code (3 letters) [optional]
    ///		
    ///		[Payments:]
    ///		Amount1 User1
    ///		Amount2 User2
    ///		...
    ///		AmountN UserN
    ///		
    ///		[Lines]
    ///		Description1 [one line]
    ///		Subtotal1
    ///		Ratio1_1 User1_1
    ///		Ratio2_1 User2_1
    ///		...
    ///		RatioK_1 UserK_1
    ///		[empty line between bill lines:]
    ///		
    ///		Description2 [one line]
    ///		Subtotal2
    ///		Ratio1_2 User1_2
    ///		Ratio2_2 User2_2
    ///		...
    ///		RatioL_2 UserL_2
    ///		...
    ///		DescriptionM [one line]
    ///		SubtotalM
    ///		Ratio1_M User1_M
    ///		Ratio2_M User2_M
    ///		...
    ///		RatioP_M UserP_M
    /// </remarks>
    /// <param name="message"></param>
    /// <returns></returns>
    [HttpPost("text")]
	[RawTextRequest]
    public IActionResult PostText([FromQuery] bool createDrafted)
    {
        try
        {
            var message = Request.Body.ReadToEndAsync().Result;
            var billGuid = _billService.AddBill(message, UserId!.Value);
            
            if(!createDrafted)
            {
				_billService.Finalize(billGuid);
            }
            return Ok(billGuid);
        }
        catch (Exception e)
        {
            return BadRequest(e.Message);
        }
    }

	[HttpPost("{id}/Finalize")]
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