using DebtBot.DB;
using DebtBot.Models.Receipt;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.Text.Json;

namespace DebtBot.Receipts;

/// <summary>Why a receipt wasn't read. The API's <c>{code}</c> for each is in <see cref="ReceiptErrors"/>.</summary>
public enum ReceiptError
{
    /// <summary>The feature is off (no API key).</summary>
    Unavailable,
    NoImage,
    TooLarge,
    UnsupportedType,
    /// <summary>A HEIC/HEIF photo: convert it to JPEG first (the web client does).</summary>
    Heic,
    RateLimited,
    NotAReceipt,
    /// <summary>The model's answer wasn't valid JSON for the schema, twice.</summary>
    Unreadable,
    /// <summary>The AI service is busy or out of quota (429 or 402).</summary>
    Busy,
    Timeout,
    /// <summary>The AI service failed (5xx, or an error retrying won't fix).</summary>
    Upstream,
}

public static class ReceiptErrors
{
    public static string Code(ReceiptError error) => error switch
    {
        ReceiptError.Unavailable => "receiptUnavailable",
        ReceiptError.NoImage => "noImage",
        ReceiptError.TooLarge => "tooLarge",
        ReceiptError.UnsupportedType => "unsupportedType",
        ReceiptError.Heic => "heicNotSupported",
        ReceiptError.RateLimited => "rateLimited",
        ReceiptError.NotAReceipt => "notAReceipt",
        ReceiptError.Unreadable => "unreadable",
        ReceiptError.Busy => "busy",
        ReceiptError.Timeout => "timeout",
        ReceiptError.Upstream => "upstreamError",
        _ => "error",
    };

    /// <summary>English messages, the same on the web and in the bot (the web words the codes itself).</summary>
    public static string Message(ReceiptError error) => error switch
    {
        ReceiptError.Unavailable => "Reading receipts isn't set up on this server.",
        ReceiptError.NoImage => "Send a photo of the receipt.",
        ReceiptError.TooLarge => "The photo is too large (5 MB at most).",
        ReceiptError.UnsupportedType => "Send the receipt as a JPEG, PNG or WebP photo.",
        ReceiptError.Heic => "HEIC photos can't be read. Send it as JPEG (on an iPhone: Settings › Camera › Formats › Most Compatible).",
        ReceiptError.RateLimited => "You've read too many receipts for now. Try later or fill the bill in by hand.",
        ReceiptError.NotAReceipt => "This doesn't look like a receipt.",
        ReceiptError.Unreadable => "The receipt couldn't be read. Try a sharper photo or fill the bill in by hand.",
        ReceiptError.Busy => "The free AI service is busy or out of quota. Try later or fill the bill in by hand.",
        ReceiptError.Timeout => "The AI service took too long. Try again or fill the bill in by hand.",
        ReceiptError.Upstream => "The AI service failed. Try again or fill the bill in by hand.",
        _ => "The receipt couldn't be read.",
    };

    public static int Status(ReceiptError error) => error switch
    {
        ReceiptError.Unavailable => StatusCodes.Status503ServiceUnavailable,
        ReceiptError.NoImage => StatusCodes.Status400BadRequest,
        ReceiptError.TooLarge => StatusCodes.Status413PayloadTooLarge,
        ReceiptError.UnsupportedType or ReceiptError.Heic => StatusCodes.Status415UnsupportedMediaType,
        ReceiptError.RateLimited => StatusCodes.Status429TooManyRequests,
        ReceiptError.NotAReceipt or ReceiptError.Unreadable => StatusCodes.Status422UnprocessableEntity,
        ReceiptError.Busy => StatusCodes.Status503ServiceUnavailable,
        ReceiptError.Timeout => StatusCodes.Status504GatewayTimeout,
        _ => StatusCodes.Status502BadGateway,
    };
}

/// <summary>A photo to read. <see cref="FileName"/> is only a hint for the fake client.</summary>
public sealed record ReceiptInput(byte[]? Image, string? FileName = null);

public sealed record ReceiptParseResult(ReceiptDraft? Draft, ReceiptError? Error = null, ReceiptLimitDecision? Limit = null)
{
    public bool Succeeded => Draft is not null;
}

/// <summary>Image checks by content, not by the declared type.</summary>
public static class ReceiptImage
{
    /// <summary>image/jpeg, image/png or image/webp, "heic" for HEIC/HEIF, or null.</summary>
    public static string? Detect(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF)
            return "image/jpeg";
        if (bytes.Length >= 8 && bytes[..8].SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }))
            return "image/png";
        if (bytes.Length >= 12 && bytes[..4].SequenceEqual("RIFF"u8) && bytes[8..12].SequenceEqual("WEBP"u8))
            return "image/webp";
        if (bytes.Length >= 12 && bytes[4..8].SequenceEqual("ftyp"u8))
        {
            var brand = System.Text.Encoding.ASCII.GetString(bytes[8..12]);
            if (brand is "heic" or "heix" or "hevc" or "hevx" or "heim" or "heis" or "mif1" or "msf1" or "avif")
                return "heic";
        }
        return null;
    }
}

public interface IReceiptService
{
    /// <summary>True when an API key is set (or the fake provider runs in Development).</summary>
    bool IsEnabled { get; }

    /// <summary>
    /// Reads a receipt photo for <paramref name="userId"/>: checks the image, takes one use of the user's limits,
    /// asks the model (retrying once on invalid JSON, 5xx or a timeout), validates and normalises the answer, and
    /// keeps the draft 15 minutes (<see cref="IReceiptDraftStore"/>). The draft carries the bill text with the user
    /// as the stand-in payer and participant.
    /// </summary>
    Task<ReceiptParseResult> ParseAsync(Guid userId, ReceiptInput input, CancellationToken cancellationToken);
}

public sealed class ReceiptService : IReceiptService
{
    private readonly ILlmClient _client;
    private readonly IReceiptRateLimiter _limiter;
    private readonly IReceiptDraftStore _drafts;
    private readonly DebtContext _db;
    private readonly ReceiptFeature _feature;
    private readonly IOptions<DebtBotConfiguration> _options;
    private readonly ILogger<ReceiptService> _logger;

    public ReceiptService(ILlmClient client, IReceiptRateLimiter limiter, IReceiptDraftStore drafts, DebtContext db,
        ReceiptFeature feature, IOptions<DebtBotConfiguration> options, ILogger<ReceiptService> logger)
    {
        _client = client;
        _limiter = limiter;
        _drafts = drafts;
        _db = db;
        _feature = feature;
        _options = options;
        _logger = logger;
    }

    public bool IsEnabled => _feature.IsEnabled;

    public async Task<ReceiptParseResult> ParseAsync(Guid userId, ReceiptInput input, CancellationToken cancellationToken)
    {
        if (!_feature.IsEnabled)
        {
            return new(null, ReceiptError.Unavailable);
        }

        var settings = _options.Value.Llm;
        if (input.Image is null || input.Image.Length == 0)
        {
            return new(null, ReceiptError.NoImage);
        }
        if (input.Image.Length > settings.MaxImageBytes)
        {
            return new(null, ReceiptError.TooLarge);
        }
        var mediaType = ReceiptImage.Detect(input.Image);
        if (mediaType == "heic")
        {
            return new(null, ReceiptError.Heic);
        }
        if (mediaType is null)
        {
            return new(null, ReceiptError.UnsupportedType);
        }

        var limit = _limiter.TryAcquire(userId);
        if (!limit.Allowed)
        {
            return new(null, ReceiptError.RateLimited, limit);
        }

        var request = new LlmRequest
        {
            Models = settings.AllModels(),
            SystemPrompt = ReceiptSchema.SystemPrompt,
            UserText = ReceiptSchema.UserText,
            Image = input.Image,
            ImageMediaType = mediaType,
            SchemaName = ReceiptSchema.Name,
            Schema = ReceiptSchema.Strict(),
            Format = string.Equals(settings.ResponseFormat, LlmConfiguration.JsonObjectFormat, StringComparison.OrdinalIgnoreCase)
                ? LlmResponseFormat.JsonObject
                : LlmResponseFormat.JsonSchema,
            Hint = input.FileName,
        };

        // One try plus at most one retry, on invalid JSON, 5xx or a timeout. Busy and other errors aren't retried.
        ReceiptError error = ReceiptError.Upstream;
        for (var attempt = 1; attempt <= 2; attempt++)
        {
            var stopwatch = Stopwatch.StartNew();
            var result = await _client.CompleteJsonAsync(request, cancellationToken);

            if (result.Outcome == LlmOutcome.Ok && result.Json is JsonElement answer)
            {
                if (ReceiptSchema.IsNotAReceipt(answer))
                {
                    Log("notAReceipt", attempt, result, stopwatch);
                    return new(null, ReceiptError.NotAReceipt);
                }
                if (ReceiptSchema.IsValid(answer))
                {
                    var draft = ReceiptNormalizer.Normalize(answer, await LastBillCurrencyAsync(userId, cancellationToken));
                    draft.Model = result.Model ?? request.Models[0];
                    draft.Text = ReceiptTextBuilder.Build(draft, await SenderAsync(userId, cancellationToken));
                    Log("ok", attempt, result, stopwatch, draft.Lines.Count);
                    return new(_drafts.Add(userId, draft));
                }
                Log("schemaInvalid", attempt, result, stopwatch);
                error = ReceiptError.Unreadable;
                continue;
            }

            Log(result.Outcome.ToString(), attempt, result, stopwatch);
            switch (result.Outcome)
            {
                case LlmOutcome.InvalidJson:
                    error = ReceiptError.Unreadable;
                    continue;
                case LlmOutcome.Timeout:
                    error = ReceiptError.Timeout;
                    continue;
                case LlmOutcome.Upstream:
                    error = ReceiptError.Upstream;
                    continue;
                case LlmOutcome.Busy:
                    return new(null, ReceiptError.Busy);
                default:
                    return new(null, ReceiptError.Upstream);
            }
        }

        return new(null, error);
    }

    private void Log(string outcome, int attempt, LlmResult result, Stopwatch stopwatch, int? lines = null) =>
        _logger.LogInformation(
            "Receipt parse: {Outcome} (attempt {Attempt}) from {Model} in {ElapsedMs} ms, tokens {PromptTokens}/{CompletionTokens}, lines {Lines}",
            outcome, attempt, result.Model, stopwatch.ElapsedMilliseconds, result.PromptTokens, result.CompletionTokens, lines);

    /// <summary>The currency of the user's latest bill, to read an ambiguous <c>$</c>.</summary>
    private Task<string?> LastBillCurrencyAsync(Guid userId, CancellationToken cancellationToken) =>
        _db.Bills
            .Where(b => b.CreatorId == userId)
            .OrderByDescending(b => b.Date)
            .Select(b => b.CurrencyCode)
            .FirstOrDefaultAsync(cancellationToken);

    private async Task<string> SenderAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await _db.Users
            .Where(u => u.Id == userId)
            .Select(u => new { u.TelegramUserName, u.TelegramId })
            .FirstOrDefaultAsync(cancellationToken);
        return ReceiptTextBuilder.Sender(user?.TelegramUserName, user?.TelegramId, userId);
    }
}
