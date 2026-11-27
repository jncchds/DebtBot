using DebtBot.Receipts;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace DebtBot.Tests;

/// <summary>Receipts T1: <see cref="OpenRouterClient"/> against a stub handler (no real API).</summary>
public class OpenRouterClientTests
{
    private const string Key = "sk-or-v1-test-key-not-real";

    private sealed class StubHandler : HttpMessageHandler
    {
        public Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> Respond { get; set; } =
            (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        public HttpRequestMessage? Request { get; private set; }
        public string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            Body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return await Respond(request, cancellationToken);
        }
    }

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<string> Lines { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Lines.Add(formatter(state, exception));
    }

    private readonly StubHandler _handler = new();
    private readonly CapturingLogger<OpenRouterClient> _logger = new();
    private readonly LlmConfiguration _settings = new()
    {
        ApiKey = Key,
        Model = "nvidia/nemotron-3.5-super-vl:free",
        FallbackModels = "google/gemma-4-31b-it:free, other/model:free",
        TimeoutSeconds = 45,
    };

    private OpenRouterClient Client() =>
        new(new HttpClient(_handler), Options.Create(new DebtBotConfiguration { Llm = _settings }), _logger);

    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3];

    private LlmRequest Request(LlmResponseFormat format = LlmResponseFormat.JsonSchema, IReadOnlyList<string>? models = null) => new()
    {
        Models = models ?? _settings.AllModels(),
        SystemPrompt = ReceiptSchema.SystemPrompt,
        UserText = ReceiptSchema.UserText,
        Image = Png,
        ImageMediaType = "image/png",
        SchemaName = ReceiptSchema.Name,
        Schema = ReceiptSchema.Strict(),
        Format = format,
    };

    private void Answer(HttpStatusCode status, string body) =>
        _handler.Respond = (_, _) => Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") });

    private static string Completion(string content, string model = "nvidia/nemotron-3.5-super-vl:free") =>
        new JsonObject
        {
            ["id"] = "gen-1",
            ["model"] = model,
            ["choices"] = new JsonArray { new JsonObject { ["message"] = new JsonObject { ["role"] = "assistant", ["content"] = content } } },
            ["usage"] = new JsonObject { ["prompt_tokens"] = 1200, ["completion_tokens"] = 150 },
        }.ToJsonString();

    [Fact]
    public async Task The_request_has_the_models_the_image_the_strict_schema_and_the_headers()
    {
        _settings.Referer = "https://debts.example.org";
        Answer(HttpStatusCode.OK, Completion(FakeLlmClient.Fixture("cafe-eur")));

        await Client().CompleteJsonAsync(Request(), CancellationToken.None);

        var request = _handler.Request!;
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("https://openrouter.ai/api/v1/chat/completions", request.RequestUri!.ToString());
        Assert.Equal("Bearer", request.Headers.Authorization!.Scheme);
        Assert.Equal(Key, request.Headers.Authorization.Parameter);
        Assert.Equal("DebtBot", request.Headers.GetValues("X-Title").Single());
        Assert.Equal("https://debts.example.org", request.Headers.GetValues("HTTP-Referer").Single());

        var body = JsonNode.Parse(_handler.Body!)!;
        Assert.Equal("nvidia/nemotron-3.5-super-vl:free", body["model"]!.GetValue<string>());
        Assert.Equal(["nvidia/nemotron-3.5-super-vl:free", "google/gemma-4-31b-it:free", "other/model:free"],
            body["models"]!.AsArray().Select(m => m!.GetValue<string>()));
        Assert.Equal(0, body["temperature"]!.GetValue<int>());

        var messages = body["messages"]!.AsArray();
        Assert.Equal("system", messages[0]!["role"]!.GetValue<string>());
        Assert.Equal(ReceiptSchema.SystemPrompt, messages[0]!["content"]!.GetValue<string>());
        var user = messages[1]!["content"]!.AsArray();
        Assert.Equal("text", user[0]!["type"]!.GetValue<string>());
        Assert.Equal("image_url", user[1]!["type"]!.GetValue<string>());
        Assert.Equal("data:image/png;base64," + Convert.ToBase64String(Png), user[1]!["image_url"]!["url"]!.GetValue<string>());

        var format = body["response_format"]!;
        Assert.Equal("json_schema", format["type"]!.GetValue<string>());
        Assert.Equal("receipt", format["json_schema"]!["name"]!.GetValue<string>());
        Assert.True(format["json_schema"]!["strict"]!.GetValue<bool>());
        Assert.True(JsonNode.DeepEquals(ReceiptSchema.Strict(), format["json_schema"]!["schema"]));
    }

    [Fact]
    public async Task One_model_sends_no_models_array_and_no_referer_header()
    {
        Answer(HttpStatusCode.OK, Completion("{}"));
        _settings.BaseUrl = "http://localhost:11434/v1/";

        await Client().CompleteJsonAsync(Request(models: ["llava"]), CancellationToken.None);

        Assert.Equal("http://localhost:11434/v1/chat/completions", _handler.Request!.RequestUri!.ToString());
        var body = JsonNode.Parse(_handler.Body!)!.AsObject();
        Assert.Equal("llava", body["model"]!.GetValue<string>());
        Assert.False(body.ContainsKey("models"));
        Assert.False(_handler.Request.Headers.Contains("HTTP-Referer"));
    }

    [Fact]
    public async Task Json_object_models_get_json_object_and_the_schema_in_the_prompt()
    {
        Answer(HttpStatusCode.OK, Completion("{}"));

        await Client().CompleteJsonAsync(Request(LlmResponseFormat.JsonObject), CancellationToken.None);

        var body = JsonNode.Parse(_handler.Body!)!;
        Assert.Equal("""{"type":"json_object"}""", body["response_format"]!.ToJsonString());
        Assert.Contains("\"isReceipt\"", body["messages"]![0]!["content"]!.GetValue<string>());
    }

    [Fact]
    public async Task An_answer_gives_the_json_the_model_and_the_tokens()
    {
        Answer(HttpStatusCode.OK, Completion(FakeLlmClient.Fixture("cafe-eur"), "google/gemma-4-31b-it:free"));

        var result = await Client().CompleteJsonAsync(Request(), CancellationToken.None);

        Assert.Equal(LlmOutcome.Ok, result.Outcome);
        Assert.Equal("google/gemma-4-31b-it:free", result.Model);
        Assert.Equal(1200, result.PromptTokens);
        Assert.Equal(150, result.CompletionTokens);
        Assert.Equal("Corner Café", result.Json!.Value.GetProperty("merchant").GetString());
    }

    [Theory]
    [InlineData("```json\n{\"isReceipt\": false, \"items\": []}\n```")]
    [InlineData("Here is the JSON: {\"isReceipt\": false, \"items\": []} Hope it helps.")]
    public async Task Fences_and_chatter_around_the_object_are_tolerated(string content)
    {
        Answer(HttpStatusCode.OK, Completion(content));

        var result = await Client().CompleteJsonAsync(Request(LlmResponseFormat.JsonObject), CancellationToken.None);

        Assert.Equal(LlmOutcome.Ok, result.Outcome);
        Assert.True(ReceiptSchema.IsNotAReceipt(result.Json!.Value));
    }

    [Theory]
    [InlineData("not json at all")]
    [InlineData("[1, 2, 3]")]
    [InlineData("{\"isReceipt\": true, \"items\": [")]
    public async Task Content_that_is_not_a_json_object_is_invalid_json(string content)
    {
        Answer(HttpStatusCode.OK, Completion(content));

        var result = await Client().CompleteJsonAsync(Request(), CancellationToken.None);

        Assert.Equal(LlmOutcome.InvalidJson, result.Outcome);
        Assert.Null(result.Json);
    }

    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests, LlmOutcome.Busy)]
    [InlineData(HttpStatusCode.PaymentRequired, LlmOutcome.Busy)]
    [InlineData(HttpStatusCode.InternalServerError, LlmOutcome.Upstream)]
    [InlineData(HttpStatusCode.BadGateway, LlmOutcome.Upstream)]
    [InlineData(HttpStatusCode.ServiceUnavailable, LlmOutcome.Upstream)]
    [InlineData(HttpStatusCode.RequestTimeout, LlmOutcome.Timeout)]
    [InlineData(HttpStatusCode.Unauthorized, LlmOutcome.Failed)]
    [InlineData(HttpStatusCode.BadRequest, LlmOutcome.Failed)]
    public async Task Errors_map_to_outcomes(HttpStatusCode status, LlmOutcome outcome)
    {
        Answer(status, """{"error":{"code":0,"message":"Rate limit exceeded","metadata":{"error_type":"rate_limit_exceeded"}}}""");

        var result = await Client().CompleteJsonAsync(Request(), CancellationToken.None);

        Assert.Equal(outcome, result.Outcome);
        Assert.Equal((int)status, result.StatusCode);
    }

    [Theory]
    [InlineData("""{"error":{"code":429,"message":"Rate limit exceeded"}}""", LlmOutcome.Busy)]
    [InlineData("""{"error":{"code":402,"message":"Insufficient credits"}}""", LlmOutcome.Busy)]
    [InlineData("""{"error":{"code":502,"message":"Provider returned error"}}""", LlmOutcome.Upstream)]
    [InlineData("""{"error":{"message":"no code"}}""", LlmOutcome.Upstream)]
    [InlineData("""{"id":"gen-1","choices":[]}""", LlmOutcome.Upstream)]
    [InlineData("""{"id":"gen-1","choices":[{"message":{"content":null}}]}""", LlmOutcome.Upstream)]
    [InlineData("<html>Bad gateway</html>", LlmOutcome.Upstream)]
    public async Task Errors_inside_a_200_are_mapped_too(string body, LlmOutcome outcome)
    {
        Answer(HttpStatusCode.OK, body);

        var result = await Client().CompleteJsonAsync(Request(), CancellationToken.None);

        Assert.Equal(outcome, result.Outcome);
    }

    [Fact]
    public async Task A_slow_answer_is_a_timeout()
    {
        _settings.TimeoutSeconds = 1;
        _handler.Respond = async (_, cancellationToken) =>
        {
            await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK);
        };

        var started = DateTime.UtcNow;
        var result = await Client().CompleteJsonAsync(Request(), CancellationToken.None);

        Assert.Equal(LlmOutcome.Timeout, result.Outcome);
        Assert.InRange(DateTime.UtcNow - started, TimeSpan.FromSeconds(0.5), TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task The_callers_cancellation_is_not_a_timeout()
    {
        _handler.Respond = async (_, cancellationToken) =>
        {
            await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK);
        };
        using var cancel = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Client().CompleteJsonAsync(Request(), cancel.Token));
    }

    [Fact]
    public async Task A_network_error_is_upstream()
    {
        _handler.Respond = (_, _) => throw new HttpRequestException("Connection refused");

        var result = await Client().CompleteJsonAsync(Request(), CancellationToken.None);

        Assert.Equal(LlmOutcome.Upstream, result.Outcome);
    }

    [Fact]
    public async Task The_log_has_no_key_no_image_and_no_output()
    {
        Answer(HttpStatusCode.OK, Completion(FakeLlmClient.Fixture("cafe-eur")));

        await Client().CompleteJsonAsync(Request(), CancellationToken.None);

        var line = Assert.Single(_logger.Lines);
        Assert.Contains("Ok", line);
        Assert.Contains("nvidia/nemotron-3.5-super-vl:free", line);
        Assert.Contains("1200/150", line);
        Assert.DoesNotContain(Key, line);
        Assert.DoesNotContain(Convert.ToBase64String(Png), line);
        Assert.DoesNotContain("Corner", line);
        Assert.DoesNotContain("Borscht", line);
    }
}
