using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace DebtBot.Receipts;

/// <summary>
/// <see cref="ILlmClient"/> over an OpenAI-compatible <c>{BaseUrl}/chat/completions</c> (OpenRouter by default).
/// Logs only timing, the model, the outcome and token counts: never the key, the image or the output.
/// </summary>
public sealed class OpenRouterClient : ILlmClient
{
    public const string Title = "DebtBot";

    private readonly HttpClient _http;
    private readonly IOptions<DebtBotConfiguration> _options;
    private readonly ILogger<OpenRouterClient> _logger;

    public OpenRouterClient(HttpClient http, IOptions<DebtBotConfiguration> options, ILogger<OpenRouterClient> logger)
    {
        _http = http;
        _options = options;
        _logger = logger;
    }

    private LlmConfiguration Settings => _options.Value.Llm;

    public async Task<LlmResult> CompleteJsonAsync(LlmRequest request, CancellationToken cancellationToken)
    {
        var settings = Settings;
        var stopwatch = Stopwatch.StartNew();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(Math.Max(1, settings.TimeoutSeconds)));

        LlmResult result;
        try
        {
            using var message = BuildHttpRequest(request, settings);
            using var response = await _http.SendAsync(message, HttpCompletionOption.ResponseContentRead, timeout.Token);
            var body = await response.Content.ReadAsStringAsync(timeout.Token);
            result = ReadResponse((int)response.StatusCode, body);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            result = LlmResult.Error(LlmOutcome.Timeout);
        }
        catch (HttpRequestException)
        {
            result = LlmResult.Error(LlmOutcome.Upstream);
        }

        _logger.LogInformation(
            "LLM call: {Outcome} ({Status}) from {Model} in {ElapsedMs} ms, tokens {PromptTokens}/{CompletionTokens}",
            result.Outcome, result.StatusCode, result.Model ?? request.Models.FirstOrDefault(), stopwatch.ElapsedMilliseconds,
            result.PromptTokens, result.CompletionTokens);
        return result;
    }

    /// <summary>The request, public for the tests.</summary>
    public static HttpRequestMessage BuildHttpRequest(LlmRequest request, LlmConfiguration settings)
    {
        var message = new HttpRequestMessage(HttpMethod.Post, settings.BaseUrl.TrimEnd('/') + "/chat/completions")
        {
            Content = new StringContent(BuildBody(request).ToJsonString(), Encoding.UTF8, "application/json"),
        };
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.ApiKey ?? "");
        if (!string.IsNullOrWhiteSpace(settings.Referer))
        {
            message.Headers.TryAddWithoutValidation("HTTP-Referer", settings.Referer);
        }
        message.Headers.TryAddWithoutValidation("X-Title", Title);
        return message;
    }

    public static JsonObject BuildBody(LlmRequest request)
    {
        var userContent = new JsonArray { new JsonObject { ["type"] = "text", ["text"] = request.UserText } };
        if (request.Image is not null)
        {
            userContent.Add(new JsonObject
            {
                ["type"] = "image_url",
                ["image_url"] = new JsonObject
                {
                    ["url"] = $"data:{request.ImageMediaType};base64,{Convert.ToBase64String(request.Image)}",
                },
            });
        }

        var body = new JsonObject
        {
            ["model"] = request.Models[0],
        };
        if (request.Models.Count > 1)
        {
            body["models"] = new JsonArray(request.Models.Select(m => (JsonNode)JsonValue.Create(m)!).ToArray());
        }
        body["messages"] = new JsonArray
        {
            new JsonObject { ["role"] = "system", ["content"] = request.SystemPrompt },
            new JsonObject { ["role"] = "user", ["content"] = userContent },
        };
        body["response_format"] = request.Format == LlmResponseFormat.JsonSchema
            ? new JsonObject
            {
                ["type"] = "json_schema",
                ["json_schema"] = new JsonObject
                {
                    ["name"] = request.SchemaName,
                    ["strict"] = true,
                    ["schema"] = request.Schema.DeepClone(),
                },
            }
            : new JsonObject { ["type"] = "json_object" };
        body["temperature"] = 0;
        return body;
    }

    /// <summary>Maps a status and body to an outcome. OpenRouter can also put an <c>error</c> in a 200.</summary>
    public static LlmResult ReadResponse(int status, string body)
    {
        JsonElement? root = null;
        try
        {
            using var document = JsonDocument.Parse(body);
            root = document.RootElement.Clone();
        }
        catch (JsonException)
        {
        }

        string? model = null;
        if (root is { ValueKind: JsonValueKind.Object } r && r.TryGetProperty("model", out var m) && m.ValueKind == JsonValueKind.String)
        {
            model = m.GetString();
        }

        var errorStatus = status;
        if (status is >= 200 and < 300 && root is { ValueKind: JsonValueKind.Object } withError
            && withError.TryGetProperty("error", out var error) && error.ValueKind == JsonValueKind.Object)
        {
            errorStatus = error.TryGetProperty("code", out var code) && code.ValueKind == JsonValueKind.Number && code.TryGetInt32(out var c) ? c : 502;
        }

        if (errorStatus is < 200 or >= 300)
        {
            var outcome = errorStatus switch
            {
                429 or 402 => LlmOutcome.Busy,
                408 => LlmOutcome.Timeout,
                >= 500 => LlmOutcome.Upstream,
                _ => LlmOutcome.Failed,
            };
            return LlmResult.Error(outcome, errorStatus, model);
        }

        if (root is not { ValueKind: JsonValueKind.Object } ok
            || !ok.TryGetProperty("choices", out var choices) || choices.ValueKind != JsonValueKind.Array || choices.GetArrayLength() == 0)
        {
            return LlmResult.Error(LlmOutcome.Upstream, status, model);
        }

        var choice = choices[0];
        string? content = null;
        if (choice.TryGetProperty("message", out var messageElement) && messageElement.ValueKind == JsonValueKind.Object
            && messageElement.TryGetProperty("content", out var contentElement) && contentElement.ValueKind == JsonValueKind.String)
        {
            content = contentElement.GetString();
        }

        int? promptTokens = null, completionTokens = null;
        if (ok.TryGetProperty("usage", out var usage) && usage.ValueKind == JsonValueKind.Object)
        {
            if (usage.TryGetProperty("prompt_tokens", out var p) && p.ValueKind == JsonValueKind.Number && p.TryGetInt32(out var pv)) promptTokens = pv;
            if (usage.TryGetProperty("completion_tokens", out var ct) && ct.ValueKind == JsonValueKind.Number && ct.TryGetInt32(out var cv)) completionTokens = cv;
        }

        if (content is null)
        {
            return new LlmResult(LlmOutcome.Upstream, model, null, promptTokens, completionTokens, status);
        }

        var json = LlmJson.ParseObject(content);
        return new LlmResult(json is null ? LlmOutcome.InvalidJson : LlmOutcome.Ok, model, json, promptTokens, completionTokens, status);
    }
}
