using DebtBot.Crutch;
using DebtBot.Extensions;
using DebtBot.Identity;
using DebtBot.Interfaces.Services;
using DebtBot.Models.Bill;
using DebtBot.Models.User;
using DebtBot.Services;
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
    private readonly IParserService _parserService;

    public BillsController(IBillService billService, IExcelService excelService, IUserService userService, IParserService parserService)
    {
        _billService = billService;
        _excelService = excelService;
        _userService = userService;
        _parserService = parserService;
    }

    [HttpGet("{id}")]
	public async Task<ActionResult<BillModel?>> GetAsync(Guid id, CancellationToken cancellationToken)
	{
		var bill = await _billService.GetAsync(id, cancellationToken);

		if (!IsAdmin && !_billService.HasAccess(UserId!.Value, bill))
		{
            return NotFound();
        }

		return Ok(bill);
    }

    [HttpGet("Own")]
    public IActionResult GetOwn(int pageNumber = 0, int? countPerPage = null)
    {
        var bills = _billService.GetCreatedByUser(UserId!.Value, pageNumber, countPerPage);

        return Ok(bills);
    }

    [Authorize(IdentityData.AdminUserPolicyName)]
    [HttpGet]
	public IActionResult Get(int pageNumber = 0, int? countPerPage = null)
	{
		return Ok(_billService.Get(pageNumber, countPerPage));
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
    public async Task<IActionResult> PostTextAsync([FromQuery] bool createDrafted, CancellationToken cancellationToken)
    {
        try
        {
            var message = Request.Body.ReadToEndAsync().Result;
            var bill = _parserService.ParseBill(UserId!.Value, message);
            if (!bill.IsValid)
            {
                return BadRequest(bill.Errors);
            }
            var billGuid = _billService.Add(bill.Result!, new UserSearchModel { Id = UserId!.Value });
            
            if(!createDrafted)
            {
				await _billService.FinalizeAsync(billGuid, cancellationToken);
            }
            return Ok(billGuid);
        }
        catch (Exception e)
        {
            return BadRequest(e.Message);
        }
    }

    [HttpPost("{id}/Finalize")]
    public async Task<IActionResult> FinalizeAsync(Guid id, CancellationToken cancellationToken)
    {
        var result = await _billService.FinalizeAsync(id, cancellationToken);
        if (!result)
        {
            return BadRequest();
        }
        return Ok();
    }

    [Authorize(IdentityData.AdminUserPolicyName)]
    [HttpPost("import/{creatorTelegramUserName}")]
    public async Task<IActionResult> ImportFileAsync(IFormFile file, string creatorTelegramUserName, CancellationToken cancellationToken)
    {
        try
        {
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
            var importedUserDictionary = _excelService.ImportUsers(file.OpenReadStream());

            var creator = _userService.FindOrAddUser(new UserSearchModel() { TelegramUserName = creatorTelegramUserName });

            Dictionary<int, Guid> userModels = [];
            foreach (var userPair in importedUserDictionary)
            {
                var user = _userService.FindOrAddUser(userPair.Value, creator);
                userModels.Add(userPair.Key, user.Id);
            }

            var bills = _excelService.Import(file.OpenReadStream(), creator.Id, userModels);
            foreach (var bill in bills)
            {
                try
                {
                    var guid = _billService.Add(bill, new UserSearchModel { Id = creator.Id });
                    if (!await _billService.FinalizeAsync(guid, cancellationToken, true))
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