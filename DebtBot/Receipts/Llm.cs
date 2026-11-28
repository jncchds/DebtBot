using System.Text.Json;
using System.Text.Json.Nodes;

namespace DebtBot.Receipts;

/// <summary>A chat model asked for one JSON object.</summary>
public interface ILlmClient
{
    Task<LlmResult> CompleteJsonAsync(LlmRequest request, CancellationToken cancellationToken);
}

public enum LlmResponseFormat
{
    /// <summary><c>response_format: json_schema</c>, strict.</summary>
    JsonSchema,
    /// <summary><c>response_format: json_object</c>; the schema is only in the prompt.</summary>
    JsonObject,
}

public sealed class LlmRequest
{
    /// <summary>[primary, ...fallbacks].</summary>
    public required IReadOnlyList<string> Models { get; init; }
    public required string SystemPrompt { get; init; }
    /// <summary>The user message's text part.</summary>
    public required string UserText { get; init; }
    /// <summary>The image, sent as a data URL, or null for a text-only request.</summary>
    public byte[]? Image { get; init; }
    public string ImageMediaType { get; init; } = "image/jpeg";
    public required string SchemaName { get; init; }
    public required JsonNode Schema { get; init; }
    public LlmResponseFormat Format { get; init; } = LlmResponseFormat.JsonSchema;
    /// <summary>Only for the fake client: which fixture to answer with (for example the uploaded file's name).</summary>
    public string? Hint { get; init; }
}

public enum LlmOutcome
{
    Ok,
    /// <summary>Our timeout passed.</summary>
    Timeout,
    /// <summary>429 (rate limited) or 402 (out of credits).</summary>
    Busy,
    /// <summary>5xx, or a 200 without an answer.</summary>
    Upstream,
    /// <summary>The answer isn't a JSON object.</summary>
    InvalidJson,
    /// <summary>Any other error (a bad key, a bad request): retrying won't help.</summary>
    Failed,
}

public sealed record LlmResult(LlmOutcome Outcome, string? Model = null, JsonElement? Json = null, int? PromptTokens = null, int? CompletionTokens = null, int? StatusCode = null)
{
    public static LlmResult Error(LlmOutcome outcome, int? statusCode = null, string? model = null) => new(outcome, model, null, null, null, statusCode);
}

public static class LlmJson
{
    /// <summary>
    /// The model's message content as a JSON object. Tolerates a <c>```json</c> fence and text around the object
    /// (json_object-only models add them); anything else is null.
    /// </summary>
    public static JsonElement? ParseObject(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return null;
        }

        var text = content.Trim();
        if (!TryParse(text, out var element))
        {
            var start = text.IndexOf('{');
            var end = text.LastIndexOf('}');
            if (start < 0 || end <= start || !TryParse(text[start..(end + 1)], out element))
            {
                return null;
            }
        }

        return element.ValueKind == JsonValueKind.Object ? element : null;
    }

    private static bool TryParse(string text, out JsonElement element)
    {
        try
        {
            using var document = JsonDocument.Parse(text);
            element = document.RootElement.Clone();
            return true;
        }
        catch (JsonException)
        {
            element = default;
            return false;
        }
    }
}
