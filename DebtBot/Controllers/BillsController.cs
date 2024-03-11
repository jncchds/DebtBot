using DebtBot.Crutch;
using DebtBot.Extensions;
using DebtBot.Identity;
using DebtBot.Interfaces.Services;
using DebtBot.Models.Bill;
using DebtBot.Models.User;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OfficeOpenXml;

namespace DebtBot.Controllers;

[Authorize]
[ApiController]
[Route("api/v1/[controller]")]
public class BillsController : DebtBotControllerBase
{
	private readonly IBillService _billService;
    private readonly IExcelService _excelService;
    private readonly IUserService _userService;

    public BillsController(IBillService billService, IExcelService excelService, IUserService userService)
    {
        _billService = billService;
        _excelService = excelService;
        _userService = userService;
    }

    [HttpGet("{id}")]
	public IActionResult Get(Guid id)
	{
		var bill = _billService.Get(id);

		if (!IsAdmin && !_billService.HasAccess(UserId!.Value, bill))
		{
			return NotFound();
		}

		return Ok(bill);
    }

    [HttpGet("Own")]
    public IActionResult GetOwn()
    {
        var bills = _billService.GetCreatedByUser(UserId!.Value);

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
		return Ok(_billService.Add(billModel, UserId!.Value));
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
            var billGuid = _billService.Add(message, UserId!.Value);
            
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

    [AllowAnonymous]
    [HttpPost("import/{creatorTelegramUserName}")]
    public IActionResult ImportFile(IFormFile file, string creatorTelegramUserName)
    {
        try
        {
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
            var importedUserDictionary = _excelService.ImportUsers(file.OpenReadStream());

            var creator = _userService.FindOrAddUser(new UserSearchModel() { TelegramUserName = creatorTelegramUserName });

            Dictionary<int, UserModel> userModels = [];
            foreach (var userPair in importedUserDictionary)
            {
                var user = _userService.FindOrAddUser(userPair.Value, creator);
                userModels.Add(userPair.Key, user);
            }

            var bills = _excelService.Import(file.OpenReadStream(), creator.Id, userModels);
            foreach (var bill in bills)
            {
                try
                {
                    var guid = _billService.Add(bill, creator);
                    if (!_billService.Finalize(guid))
                        Console.WriteLine($"Failed to finalize bill {guid}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }
            }

            return Ok();
        }
        catch (Exception e)
        {
            return BadRequest(e.Message);
        }
    }
}