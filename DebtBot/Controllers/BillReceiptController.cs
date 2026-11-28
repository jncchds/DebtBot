using DebtBot.Crutch;
using DebtBot.Models.Receipt;
using DebtBot.Receipts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Globalization;

namespace DebtBot.Controllers;

/// <summary>
/// Reading a receipt photo into a bill draft. Errors are <c>{ code, message }</c> (codes in
/// <see cref="ReceiptErrors"/>). Read-only API tokens can't post (the global scope check), as a receipt costs quota.
/// </summary>
[Authorize]
[ApiController]
[Route("api/v1/Bills/receipt")]
public class BillReceiptController : DebtBotControllerBase
{
    /// <summary>The 5 MB image plus room for the multipart framing.</summary>
    public const long MaxRequestBytes = 5 * 1024 * 1024 + 64 * 1024;

    private readonly IReceiptService _receipts;
    private readonly IReceiptDraftStore _drafts;

    public BillReceiptController(IReceiptService receipts, IReceiptDraftStore drafts)
    {
        _receipts = receipts;
        _drafts = drafts;
    }

    /// <summary>
    /// Reads a receipt photo (multipart field <c>image</c>: JPEG, PNG or WebP, at most 5 MB) and returns a
    /// <c>ReceiptDraft</c>: the New bill form's fields, <c>warnings</c>, <c>model</c>, <c>text</c> (the bill text
    /// format, with you as the stand-in payer and participant) and the <c>id</c> to fetch it again for 15 minutes.
    /// Nothing is saved as a bill. Limits per user: <c>Llm:UserHourlyLimit</c> and <c>Llm:UserDailyLimit</c>.
    /// </summary>
    /// <response code="400">noImage</response>
    /// <response code="413">tooLarge</response>
    /// <response code="415">unsupportedType, heicNotSupported</response>
    /// <response code="422">notAReceipt, unreadable</response>
    /// <response code="429">rateLimited (with <c>window</c>: hour or day, <c>retryAfterSeconds</c> and a Retry-After header)</response>
    /// <response code="502">upstreamError</response>
    /// <response code="503">receiptUnavailable (the feature is off), busy (the AI service is busy or out of quota)</response>
    /// <response code="504">timeout</response>
    [HttpPost]
    [ImageUploadRequest]
    [DisableFormValueModelBinding]
    [RequestSizeLimit(MaxRequestBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxRequestBytes)]
    [ProducesResponseType<ReceiptDraft>(StatusCodes.Status200OK)]
    public async Task<IActionResult> PostAsync(CancellationToken cancellationToken)
    {
        if (!_receipts.IsEnabled)
        {
            return Error(ReceiptError.Unavailable);
        }

        if (Request.ContentLength > MaxRequestBytes)
        {
            return Error(ReceiptError.TooLarge);
        }

        if (!Request.HasFormContentType)
        {
            return Error(ReceiptError.NoImage);
        }

        IFormFile? file;
        try
        {
            var form = await Request.ReadFormAsync(cancellationToken);
            file = form.Files.GetFile("image");
        }
        catch (BadHttpRequestException e) when (e.StatusCode == StatusCodes.Status413PayloadTooLarge)
        {
            return Error(ReceiptError.TooLarge);
        }
        catch (InvalidDataException)
        {
            // The multipart body is over MultipartBodyLengthLimit, or broken.
            return Error(ReceiptError.TooLarge);
        }

        if (file is null || file.Length == 0)
        {
            return Error(ReceiptError.NoImage);
        }

        if (file.Length > MaxRequestBytes)
        {
            return Error(ReceiptError.TooLarge);
        }

        byte[] image;
        using (var buffer = new MemoryStream((int)file.Length))
        {
            await file.CopyToAsync(buffer, cancellationToken);
            image = buffer.ToArray();
        }

        var result = await _receipts.ParseAsync(UserId!.Value, new ReceiptInput(image, file.FileName), cancellationToken);
        if (result.Succeeded)
        {
            return Ok(result.Draft);
        }

        if (result.Error == ReceiptError.RateLimited && result.Limit is { } limit)
        {
            var seconds = (int)Math.Ceiling((limit.RetryAfter ?? TimeSpan.Zero).TotalSeconds);
            Response.Headers.RetryAfter = seconds.ToString(CultureInfo.InvariantCulture);
            return StatusCode(StatusCodes.Status429TooManyRequests, new
            {
                code = ReceiptErrors.Code(ReceiptError.RateLimited),
                message = ReceiptErrors.Message(ReceiptError.RateLimited),
                window = limit.Window == ReceiptLimitWindow.Day ? "day" : "hour",
                retryAfterSeconds = seconds,
            });
        }

        return Error(result.Error ?? ReceiptError.Upstream);
    }

    /// <summary>A receipt draft read in the last 15 minutes, for its owner only (404 for anyone else, or after expiry).</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType<ReceiptDraft>(StatusCodes.Status200OK)]
    public IActionResult Get(Guid id)
    {
        var draft = _drafts.Get(UserId!.Value, id);
        return draft is null
            ? StatusCode(StatusCodes.Status404NotFound, new { code = "notFound", message = "This receipt has expired or isn't yours." })
            : Ok(draft);
    }

    private IActionResult Error(ReceiptError error) =>
        StatusCode(ReceiptErrors.Status(error), new { code = ReceiptErrors.Code(error), message = ReceiptErrors.Message(error) });
}
