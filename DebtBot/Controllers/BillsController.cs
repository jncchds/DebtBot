using DebtBot.Crutch;
using DebtBot.Extensions;
using DebtBot.Identity;
using DebtBot.Interfaces.Services;
using DebtBot.Models;
using DebtBot.Models.Bill;
using DebtBot.Models.BillLine;
using DebtBot.Models.Enums;
using DebtBot.Models.Exchange;
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
    private readonly IUserContactService _contacts;

    public BillsController(IBillService billService, IExcelService excelService, IUserService userService, IParserService parserService, IUserContactService contacts)
    {
        _billService = billService;
        _excelService = excelService;
        _userService = userService;
        _parserService = parserService;
        _contacts = contacts;
    }

    [HttpGet("{id:guid}")]
	public async Task<IActionResult> GetAsync(Guid id, CancellationToken cancellationToken)
	{
		var bill = await _billService.GetAsync(id, cancellationToken);

		if (bill is null || (!IsAdmin && !_billService.CanRead(UserId!.Value, bill)))
		{
			return ErrorResult(StatusCodes.Status404NotFound, "Not found.");
		}

		return Ok(bill);
    }

    /// <summary>
    /// Bills I take part in (creator, payer or line participant). By default newest first.
    /// Query: <c>withUserId</c>, <c>currencyCode</c> (payment currency, any case), <c>status</c>, <c>createdByMe</c>,
    /// <c>dateFrom</c>, <c>dateTo</c> (whole day, UTC), <c>search</c> (description contains, ignoring case),
    /// <c>sortBy</c> (Date, Description, Total, Status), <c>sortOrder</c> (Asc, Desc), <c>pageNumber</c>, <c>countPerPage</c>.
    /// <c>ofUserId</c>: someone you have a confirmed subscription to (else 403); the bills they take part in, with
    /// <c>createdByMe</c> and <c>withUserId</c> meant from their side.
    /// </summary>
    [HttpGet]
    public IActionResult Get([FromQuery] BillListQuery query)
    {
        var owner = ListOwner(_contacts, query.OfUserId, out var denied);
        if (denied is not null)
        {
            return denied;
        }

        query.CurrencyCode = string.IsNullOrWhiteSpace(query.CurrencyCode) ? null : query.CurrencyCode.Trim().ToUpperInvariant();
        query.WithIncludesUnfinalized = true;

        return Ok(_billService.GetForUser(owner, query));
    }

    [HttpGet("Own")]
    public IActionResult GetOwn(int pageNumber = 0, int? countPerPage = null)
    {
        var bills = _billService.GetCreatedByUser(UserId!.Value, pageNumber, countPerPage);

        return Ok(bills);
    }

    /// <summary>
    /// Every bill (admin). The same query as <c>GET Bills</c> without <c>ofUserId</c> and <c>createdByMe</c>, plus
    /// <c>creatorId</c>; <c>withUserId</c> is anyone taking part in the bill.
    /// </summary>
    [Authorize(IdentityData.AdminUserPolicyName)]
    [HttpGet("All")]
	public IActionResult GetAll([FromQuery] AdminBillListQuery query)
	{
        query.CurrencyCode = string.IsNullOrWhiteSpace(query.CurrencyCode) ? null : query.CurrencyCode.Trim().ToUpperInvariant();

		return Ok(_billService.GetAll(query));
	}

    /// <summary>Creates a draft bill from the form. Returns the new bill's id.</summary>
	[HttpPost]
	public IActionResult Post(BillCreationModel billModel)
	{
		return FromResult(_billService.Create(billModel, UserId!.Value), id => Ok(id));
	}

    /// <summary>
    /// Replaces a draft bill (creator or admin): its fields, lines and payments, as in POST Bills. An empty date keeps
    /// the bill's date. 409 when the bill is no longer a draft; 400 for an exchange. Returns the bill.
    /// </summary>
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> ReplaceDraftAsync(Guid id, BillCreationModel billModel, CancellationToken cancellationToken)
    {
        var result = await _billService.ReplaceDraftAsync(id, billModel, UserId!.Value, IsAdmin, cancellationToken);
        return FromResult(result, bill => Ok(bill));
    }

    /// <summary>Adds lines to a draft bill (creator or admin). Returns the bill.</summary>
    [HttpPost("{id:guid}/lines")]
    public async Task<IActionResult> AddLinesAsync(Guid id, List<BillLineCreationModel> lines, CancellationToken cancellationToken)
    {
        var result = await _billService.AddLinesAsync(id, lines, UserId!.Value, IsAdmin, cancellationToken);
        return FromResult(result, bill => Ok(bill));
    }

    /// <summary>Adds payments to a draft bill (creator or admin). A user already paying gets the new amount. Returns the bill.</summary>
    [HttpPost("{id:guid}/payments")]
    public async Task<IActionResult> AddPaymentsAsync(Guid id, List<BillPaymentCreationModel> payments, CancellationToken cancellationToken)
    {
        var result = await _billService.AddPaymentsAsync(id, payments, UserId!.Value, IsAdmin, cancellationToken);
        return FromResult(result, bill => Ok(bill));
    }

    [HttpGet("lines/{lineId:guid}")]
    public async Task<IActionResult> GetLineAsync(Guid lineId, CancellationToken cancellationToken)
    {
        var result = await _billService.GetLineAsync(lineId, UserId!.Value, IsAdmin, cancellationToken);
        return FromResult(result, line => Ok(line));
    }

    /// <summary>
    /// Parses a bill from text (same format as <c>POST text</c>) without saving it.
    /// Always 200: <c>{ result, errors, isValid }</c>.
    /// </summary>
    [HttpPost("text/preview")]
    [RawTextRequest]
    public async Task<IActionResult> PreviewTextAsync()
    {
        var message = await Request.Body.ReadToEndAsync();
        var bill = _parserService.ParseBill(UserId!.Value, message);
        return Ok(bill);
    }

    /// <summary>
    /// Exchanges you created or are one of the two people in, one row per pair of bills. By default newest first.
    /// Query: <c>withUserId</c>, <c>currencyCode</c> (either side, any case), <c>status</c> (Draft, Processed incl. Ready,
    /// Cancelled), <c>sortBy</c> (Date, Amount), <c>sortOrder</c> (Asc, Desc), <c>pageNumber</c>, <c>countPerPage</c>.
    /// </summary>
    [HttpGet("exchanges")]
    public IActionResult GetExchanges([FromQuery] ExchangeListQuery query)
    {
        query.CurrencyCode = string.IsNullOrWhiteSpace(query.CurrencyCode) ? null : query.CurrencyCode.Trim().ToUpperInvariant();

        return Ok(_billService.GetExchangesForUser(UserId!.Value, query));
    }

    /// <summary>Creates an exchange as two draft bills; accept it with <c>POST exchange/finalize</c>.</summary>
    [HttpPost("exchange")]
    public IActionResult CreateExchange(ExchangeCreationModel exchange)
    {
        return FromResult(_billService.CreateExchange(exchange, UserId!.Value), pair => Ok(pair));
    }

    /// <summary>Accepts a draft exchange: finalizes both bills (creator or admin).</summary>
    [HttpPost("exchange/finalize")]
    public async Task<IActionResult> FinalizeExchangeAsync(ExchangeModel exchange, CancellationToken cancellationToken)
    {
        var result = await _billService.FinalizeExchangeAsync(exchange.ForwardBillId, exchange.BackwardBillId, UserId!.Value, IsAdmin, cancellationToken);
        return FromResult(result, pair => Ok(pair));
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

    /// <summary>Finalizes a draft bill (creator or admin). The ledger is written in the background.</summary>
    [HttpPost("{id:guid}/Finalize")]
    public async Task<IActionResult> FinalizeAsync(Guid id, CancellationToken cancellationToken)
    {
        var result = await _billService.FinalizeAsync(id, UserId!.Value, IsAdmin, cancellationToken);
        return FromResult(result, _ => Ok());
    }

    /// <summary>Cancels a bill (creator or admin); for an exchange, its other bill too. Runs in the background.</summary>
    [HttpPost("{id:guid}/Cancel")]
    public async Task<IActionResult> CancelAsync(Guid id, CancellationToken cancellationToken)
    {
        var result = await _billService.CancelAsync(id, UserId!.Value, IsAdmin, cancellationToken);
        return FromResult(result, _ => Ok());
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