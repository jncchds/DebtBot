using DebtBot.DB.Entities;
using DebtBot.Models.Bill;
using DebtBot.Models.Enums;
using DebtBot.Receipts;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace DebtBot.Tests;

/// <summary>
/// Receipts T1: <c>POST Bills/receipt</c>, <c>GET Bills/receipt/{id}</c>, <c>GET auth/config</c> and the Profile
/// setting, on the real pipeline with the fake LLM client.
/// </summary>
public sealed class ReceiptEndpointTests : IAsyncDisposable
{
    private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 0, 0x10, (byte)'J', (byte)'F', (byte)'I', (byte)'F', 0, 1];

    private ApiTokenHost _h;
    private User _anna = null!;
    private User _boris = null!;

    public ReceiptEndpointTests()
    {
        _h = Start();
    }

    private ApiTokenHost Start(IDictionary<string, string?>? settings = null)
    {
        var all = new Dictionary<string, string?>
        {
            ["DebtBot:Llm:Provider"] = "Fake",
            ["DebtBot:Llm:UserHourlyLimit"] = "100",
            ["DebtBot:Llm:UserDailyLimit"] = "100",
        };
        foreach (var (key, value) in settings ?? new Dictionary<string, string?>())
        {
            all[key] = value;
        }

        var host = ApiTokenHost.Start(settings: all);
        _anna = host.F.AddUser("Anna", telegramId: 1001, telegramUserName: "@anna");
        _boris = host.F.AddUser("Boris", telegramId: 1002);
        return host;
    }

    private async Task Restart(IDictionary<string, string?> settings)
    {
        await _h.DisposeAsync();
        _h = Start(settings);
    }

    public ValueTask DisposeAsync() => _h.DisposeAsync();

    private FakeLlmClient Fake => _h.Fake!;

    private static async Task<JsonElement> Json(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.Clone();

    private Task<HttpResponseMessage> Post(string? bearer, byte[]? image = null, string fileName = "receipt.jpg", string field = "image")
    {
        var form = new MultipartFormDataContent();
        if (image is not null)
        {
            var content = new ByteArrayContent(image);
            content.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
            form.Add(content, field, fileName);
        }
        else
        {
            form.Add(new StringContent("x"), "note");
        }

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/Bills/receipt") { Content = form };
        if (bearer is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
        }
        return _h.Client.SendAsync(request);
    }

    private async Task<string> Code(HttpResponseMessage response) => (await Json(response)).GetProperty("code").GetString()!;

    // --- the feature flag ---

    [Fact]
    public async Task Config_says_receipts_are_on_with_the_model()
    {
        var config = await Json(await _h.Send(HttpMethod.Get, "/api/v1/auth/config", null));
        Assert.True(config.GetProperty("receiptParse").GetBoolean());
        Assert.Equal(FakeLlmClient.ModelName, config.GetProperty("receiptModel").GetString());
    }

    [Fact]
    public async Task Without_a_key_receipts_are_off()
    {
        await Restart(new Dictionary<string, string?> { ["DebtBot:Llm:Provider"] = "OpenRouter", ["DebtBot:Llm:ApiKey"] = "" });

        var config = await Json(await _h.Send(HttpMethod.Get, "/api/v1/auth/config", null));
        Assert.False(config.GetProperty("receiptParse").GetBoolean());
        Assert.Equal(JsonValueKind.Null, config.GetProperty("receiptModel").ValueKind);

        var response = await Post(_h.Jwt(_anna), Jpeg);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("receiptUnavailable", await Code(response));
    }

    [Fact]
    public async Task With_a_key_receipts_are_on_with_the_configured_model()
    {
        await Restart(new Dictionary<string, string?> { ["DebtBot:Llm:Provider"] = "OpenRouter", ["DebtBot:Llm:ApiKey"] = "sk-or-test" });

        var config = await Json(await _h.Send(HttpMethod.Get, "/api/v1/auth/config", null));
        Assert.True(config.GetProperty("receiptParse").GetBoolean());
        Assert.Equal("nvidia/nemotron-3.5-super-vl:free", config.GetProperty("receiptModel").GetString());
        Assert.IsType<OpenRouterClient>(_h.Services.GetRequiredService<ILlmClient>());
    }

    [Fact]
    public void The_fake_never_runs_outside_development()
    {
        var options = Microsoft.Extensions.Options.Options.Create(new DebtBotConfiguration { Llm = new() { Provider = "Fake" } });
        var production = NSubstitute.Substitute.For<Microsoft.Extensions.Hosting.IHostEnvironment>();
        production.EnvironmentName.Returns("Production");
        Assert.False(new ReceiptFeature(options, production).IsEnabled);

        var development = NSubstitute.Substitute.For<Microsoft.Extensions.Hosting.IHostEnvironment>();
        development.EnvironmentName.Returns("Development");
        Assert.True(new ReceiptFeature(options, development).IsEnabled);

        Assert.Equal("OpenRouter", new LlmConfiguration().Provider); // the default is never the fake
    }

    // --- who may post ---

    [Fact]
    public async Task Anonymous_gets_401()
    {
        var response = await Post(null, Jpeg);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Empty(Fake.Requests);
    }

    [Fact]
    public async Task A_read_only_token_is_refused_and_a_read_write_token_works()
    {
        var readOnly = await Post(_h.Pat(_anna, ApiTokenScope.Read), Jpeg);
        Assert.Equal(HttpStatusCode.Forbidden, readOnly.StatusCode);
        Assert.Empty(Fake.Requests);

        var readWrite = await Post(_h.Pat(_anna, ApiTokenScope.ReadWrite), Jpeg);
        Assert.Equal(HttpStatusCode.OK, readWrite.StatusCode);
    }

    // --- the draft ---

    [Fact]
    public async Task A_photo_gives_a_draft_with_text_and_an_id()
    {
        var response = await Post(_h.Jwt(_anna), Jpeg, "cafe-eur.jpg");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var draft = await Json(response);
        Assert.Equal("Corner Café", draft.GetProperty("description").GetString());
        Assert.Equal("EUR", draft.GetProperty("currencyCode").GetString());
        Assert.Equal(39.50m, draft.GetProperty("totalWithTips").GetDecimal());
        Assert.Equal(4, draft.GetProperty("lines").GetArrayLength());
        Assert.Equal("Borscht ×2", draft.GetProperty("lines")[0].GetProperty("description").GetString());
        Assert.Equal(13.80m, draft.GetProperty("lines")[0].GetProperty("subtotal").GetDecimal());
        Assert.Equal(0, draft.GetProperty("warnings").GetArrayLength());
        Assert.Equal(FakeLlmClient.ModelName, draft.GetProperty("model").GetString());
        Assert.StartsWith("Corner Café\n\n39.50\nEUR\n\n39.50 @anna\n\nBorscht ×2\n13.80\n1 @anna", draft.GetProperty("text").GetString());
        Assert.NotEqual(Guid.Empty, draft.GetProperty("id").GetGuid());

        // What the model got: the image as JPEG, the strict schema, the hint.
        var request = Assert.Single(Fake.Requests);
        Assert.Equal("image/jpeg", request.ImageMediaType);
        Assert.Equal(Jpeg, request.Image);
        Assert.Equal(LlmResponseFormat.JsonSchema, request.Format);
        Assert.Equal(["nvidia/nemotron-3.5-super-vl:free", "google/gemma-4-31b-it:free"], request.Models);
        Assert.Equal("cafe-eur.jpg", request.Hint);
    }

    [Fact]
    public async Task Warnings_come_as_codes_with_params()
    {
        var draft = await Json(await Post(_h.Jwt(_anna), Jpeg, "tips-outside.jpg"));
        var warning = Assert.Single(draft.GetProperty("warnings").EnumerateArray());
        Assert.Equal("tipsAdded", warning.GetProperty("code").GetString());
        Assert.Equal("4.00", warning.GetProperty("params").GetProperty("amount").GetString());
        Assert.Equal(35.00m, draft.GetProperty("totalWithTips").GetDecimal());
    }

    [Fact]
    public async Task An_ambiguous_dollar_uses_the_users_last_bill_currency()
    {
        var created = _h.F.BillService.Create(new BillCreationModel { CurrencyCode = "CAD", Description = "Earlier" }, _anna.Id);
        Assert.Equal(Services.BillActionStatus.Ok, created.Status);

        var draft = await Json(await Post(_h.Jwt(_anna), Jpeg, "dollar-ambiguous.jpg"));
        Assert.Equal("CAD", draft.GetProperty("currencyCode").GetString());

        var other = await Json(await Post(_h.Jwt(_boris), Jpeg, "dollar-ambiguous.jpg"));
        Assert.Equal(JsonValueKind.Null, other.GetProperty("currencyCode").ValueKind);
        Assert.Equal("currencyAmbiguous", other.GetProperty("warnings")[0].GetProperty("code").GetString());
        // Boris has no Telegram username: his id stands in.
        Assert.Contains("\n1 1002", other.GetProperty("text").GetString());
    }

    [Fact]
    public async Task The_owner_gets_the_draft_back_and_nobody_else()
    {
        var id = (await Json(await Post(_h.Jwt(_anna), Jpeg))).GetProperty("id").GetGuid();

        var mine = await _h.Send(HttpMethod.Get, $"/api/v1/Bills/receipt/{id}", _h.Jwt(_anna));
        Assert.Equal(HttpStatusCode.OK, mine.StatusCode);
        Assert.Equal("Corner Café", (await Json(mine)).GetProperty("description").GetString());

        // A read-only token may read it.
        var token = await _h.Send(HttpMethod.Get, $"/api/v1/Bills/receipt/{id}", _h.Pat(_anna, ApiTokenScope.Read));
        Assert.Equal(HttpStatusCode.OK, token.StatusCode);

        var boris = await _h.Send(HttpMethod.Get, $"/api/v1/Bills/receipt/{id}", _h.Jwt(_boris));
        Assert.Equal(HttpStatusCode.NotFound, boris.StatusCode);
        Assert.Equal("notFound", await Code(boris));

        var unknown = await _h.Send(HttpMethod.Get, $"/api/v1/Bills/receipt/{Guid.NewGuid()}", _h.Jwt(_anna));
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);

        var anonymous = await _h.Send(HttpMethod.Get, $"/api/v1/Bills/receipt/{id}", null);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
    }

    [Fact]
    public async Task A_draft_expires_after_15_minutes()
    {
        var draft = await Json(await Post(_h.Jwt(_anna), Jpeg));
        var id = draft.GetProperty("id").GetGuid();
        Assert.Equal(_h.Time.Now.UtcDateTime.AddMinutes(15), draft.GetProperty("expiresAt").GetDateTime().ToUniversalTime());

        _h.Time.Advance(TimeSpan.FromMinutes(14));
        Assert.Equal(HttpStatusCode.OK, (await _h.Send(HttpMethod.Get, $"/api/v1/Bills/receipt/{id}", _h.Jwt(_anna))).StatusCode);

        _h.Time.Advance(TimeSpan.FromMinutes(1));
        Assert.Equal(HttpStatusCode.NotFound, (await _h.Send(HttpMethod.Get, $"/api/v1/Bills/receipt/{id}", _h.Jwt(_anna))).StatusCode);
    }

    [Fact]
    public async Task The_draft_store_keeps_no_image()
    {
        var store = _h.Services.GetRequiredService<IReceiptDraftStore>();
        var id = (await Json(await Post(_h.Jwt(_anna), Jpeg))).GetProperty("id").GetGuid();
        var draft = store.Get(_anna.Id, id)!;
        Assert.DoesNotContain(typeof(Models.Receipt.ReceiptDraft).GetProperties(), p => p.PropertyType == typeof(byte[]));
        Assert.NotNull(draft);
    }

    // --- limits ---

    [Fact]
    public async Task The_hourly_and_daily_limits_apply_per_user()
    {
        await Restart(new Dictionary<string, string?> { ["DebtBot:Llm:UserHourlyLimit"] = "2", ["DebtBot:Llm:UserDailyLimit"] = "3" });
        var anna = _h.Jwt(_anna);

        Assert.Equal(HttpStatusCode.OK, (await Post(anna, Jpeg)).StatusCode);
        _h.Time.Advance(TimeSpan.FromMinutes(10));
        Assert.Equal(HttpStatusCode.OK, (await Post(anna, Jpeg)).StatusCode);

        var hour = await Post(anna, Jpeg);
        Assert.Equal(HttpStatusCode.TooManyRequests, hour.StatusCode);
        var body = await Json(hour);
        Assert.Equal("rateLimited", body.GetProperty("code").GetString());
        Assert.Equal("hour", body.GetProperty("window").GetString());
        Assert.Equal(50 * 60, body.GetProperty("retryAfterSeconds").GetInt32());
        Assert.Equal("3000", hour.Headers.GetValues("Retry-After").Single());

        // Another user isn't affected, nor is a token of the same user exempt.
        Assert.Equal(HttpStatusCode.OK, (await Post(_h.Jwt(_boris), Jpeg)).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await Post(_h.Pat(_anna), Jpeg)).StatusCode);

        _h.Time.Advance(TimeSpan.FromMinutes(51));
        Assert.Equal(HttpStatusCode.OK, (await Post(anna, Jpeg)).StatusCode);

        _h.Time.Advance(TimeSpan.FromHours(2));
        var day = await Post(anna, Jpeg);
        Assert.Equal(HttpStatusCode.TooManyRequests, day.StatusCode);
        Assert.Equal("day", (await Json(day)).GetProperty("window").GetString());

        _h.Time.Advance(TimeSpan.FromDays(1));
        Assert.Equal(HttpStatusCode.OK, (await Post(anna, Jpeg)).StatusCode);

        // Refused calls never reached the model: 3 + 1 (Boris) + 1 = 5 requests.
        Assert.Equal(5, Fake.Requests.Count);
    }

    [Fact]
    public void The_limiter_is_a_service_the_bot_can_call()
    {
        var limiter = _h.Services.GetRequiredService<IReceiptRateLimiter>();
        for (var i = 0; i < 100; i++)
        {
            Assert.True(limiter.TryAcquire(_anna.Id).Allowed);
        }
        var refused = limiter.TryAcquire(_anna.Id);
        Assert.False(refused.Allowed);
        Assert.Equal(ReceiptLimitWindow.Day, refused.Window);
    }

    [Fact]
    public async Task Images_over_5_mb_are_too_large()
    {
        var justFits = new byte[5 * 1024 * 1024];
        Jpeg.CopyTo(justFits, 0);
        Assert.Equal(HttpStatusCode.OK, (await Post(_h.Jwt(_anna), justFits)).StatusCode);

        var over = new byte[5 * 1024 * 1024 + 1];
        Jpeg.CopyTo(over, 0);
        var response = await Post(_h.Jwt(_anna), over);
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
        Assert.Equal("tooLarge", await Code(response));

        var wayOver = new byte[6 * 1024 * 1024];
        Jpeg.CopyTo(wayOver, 0);
        var big = await Post(_h.Jwt(_anna), wayOver);
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, big.StatusCode);
        Assert.Equal("tooLarge", await Code(big));

        Assert.Single(Fake.Requests);
    }

    public static TheoryData<byte[], string> Types => new()
    {
        { [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0], "image/png" },
        { "RIFF\0\0\0\0WEBPVP8 "u8.ToArray(), "image/webp" },
        { [0xFF, 0xD8, 0xFF, 0xDB, 0, 0], "image/jpeg" },
    };

    [Theory]
    [MemberData(nameof(Types))]
    public async Task Png_webp_and_jpeg_are_read_by_content(byte[] image, string mediaType)
    {
        var response = await Post(_h.Jwt(_anna), image, "photo.bin");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(mediaType, Assert.Single(Fake.Requests).ImageMediaType);
    }

    [Fact]
    public async Task Heic_is_refused_with_a_hint()
    {
        var heic = "\0\0\0\u0018ftypheic\0\0\0\0"u8.ToArray();
        var response = await Post(_h.Jwt(_anna), heic, "IMG_0001.HEIC");
        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
        var body = await Json(response);
        Assert.Equal("heicNotSupported", body.GetProperty("code").GetString());
        Assert.Contains("JPEG", body.GetProperty("message").GetString());
    }

    [Fact]
    public async Task Other_files_are_refused()
    {
        var gif = await Post(_h.Jwt(_anna), "GIF89a\u0001\0"u8.ToArray(), "a.jpg");
        Assert.Equal(HttpStatusCode.UnsupportedMediaType, gif.StatusCode);
        Assert.Equal("unsupportedType", await Code(gif));

        var none = await Post(_h.Jwt(_anna));
        Assert.Equal(HttpStatusCode.BadRequest, none.StatusCode);
        Assert.Equal("noImage", await Code(none));

        var wrongField = await Post(_h.Jwt(_anna), Jpeg, field: "photo");
        Assert.Equal("noImage", await Code(wrongField));

        var json = await _h.Send(HttpMethod.Post, "/api/v1/Bills/receipt", _h.Jwt(_anna), new { image = "x" });
        Assert.Equal(HttpStatusCode.BadRequest, json.StatusCode);
        Assert.Equal("noImage", await Code(json));

        Assert.Empty(Fake.Requests);
    }

    // --- errors and the one retry ---

    [Fact]
    public async Task Not_a_receipt_is_422()
    {
        var response = await Post(_h.Jwt(_anna), Jpeg, "not-a-receipt.jpg");
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("notAReceipt", await Code(response));
        Assert.Single(Fake.Requests); // not retried
    }

    [Fact]
    public async Task Broken_json_is_retried_once_then_unreadable()
    {
        var response = await Post(_h.Jwt(_anna), Jpeg, "broken-json.jpg");
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("unreadable", await Code(response));
        Assert.Equal(2, Fake.Requests.Count);
    }

    [Fact]
    public async Task An_answer_off_the_schema_is_retried_like_broken_json()
    {
        Fake.Enqueue(FakeLlmClient.FromContent("""{ "isReceipt": true, "items": "none" }"""));
        Fake.EnqueueFixture("cafe-eur");

        var response = await Post(_h.Jwt(_anna), Jpeg);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, Fake.Requests.Count);
    }

    [Fact]
    public async Task The_retry_can_succeed()
    {
        Fake.EnqueueFixture("broken-json");
        Fake.EnqueueFixture("supermarket-discount");

        var response = await Post(_h.Jwt(_anna), Jpeg);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("FreshMart 2026-11-21", (await Json(response)).GetProperty("description").GetString());
        Assert.Equal(2, Fake.Requests.Count);
    }

    [Theory]
    [InlineData(LlmOutcome.Upstream, HttpStatusCode.BadGateway, "upstreamError")]
    [InlineData(LlmOutcome.Timeout, HttpStatusCode.GatewayTimeout, "timeout")]
    public async Task Upstream_errors_and_timeouts_are_retried_once(LlmOutcome outcome, HttpStatusCode status, string code)
    {
        Fake.Enqueue(LlmResult.Error(outcome));
        Fake.Enqueue(LlmResult.Error(outcome));
        Fake.EnqueueFixture("cafe-eur"); // never reached: no second retry

        var response = await Post(_h.Jwt(_anna), Jpeg);
        Assert.Equal(status, response.StatusCode);
        Assert.Equal(code, await Code(response));
        Assert.Equal(2, Fake.Requests.Count);

        Fake.Enqueue(LlmResult.Error(outcome));
        Assert.Equal(HttpStatusCode.OK, (await Post(_h.Jwt(_anna), Jpeg)).StatusCode); // the fixture left in the queue answers
    }

    [Fact]
    public async Task Busy_or_out_of_quota_is_not_retried()
    {
        var response = await Post(_h.Jwt(_anna), Jpeg, "busy.jpg");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var body = await Json(response);
        Assert.Equal("busy", body.GetProperty("code").GetString());
        Assert.Equal("The free AI service is busy or out of quota. Try later or fill the bill in by hand.", body.GetProperty("message").GetString());
        Assert.Single(Fake.Requests);
    }

    [Fact]
    public async Task Other_failures_are_upstream_errors_without_a_retry()
    {
        Fake.Enqueue(LlmResult.Error(LlmOutcome.Failed, 401));
        var response = await Post(_h.Jwt(_anna), Jpeg);
        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        Assert.Equal("upstreamError", await Code(response));
        Assert.Single(Fake.Requests);
    }

    [Fact]
    public async Task Json_object_mode_is_a_setting()
    {
        await Restart(new Dictionary<string, string?> { ["DebtBot:Llm:ResponseFormat"] = "json_object" });
        await Post(_h.Jwt(_anna), Jpeg);
        Assert.Equal(LlmResponseFormat.JsonObject, Assert.Single(Fake.Requests).Format);
    }

    // --- the Profile setting ---

    [Fact]
    public async Task Receipts_open_in_round_trips_through_the_profile()
    {
        var jwt = _h.Jwt(_anna);
        var profile = await Json(await _h.Send(HttpMethod.Get, "/api/v1/profile", jwt));
        Assert.Equal("Form", profile.GetProperty("receiptOpenIn").GetString());

        var set = await _h.Send(HttpMethod.Put, "/api/v1/profile", jwt, new { displayName = "Anna", receiptOpenIn = "Text" });
        Assert.Equal(HttpStatusCode.OK, set.StatusCode);
        Assert.Equal("Text", (await Json(set)).GetProperty("receiptOpenIn").GetString());

        // The existing Profile form doesn't send it: the setting stays.
        var other = await _h.Send(HttpMethod.Put, "/api/v1/profile", jwt, new { displayName = "Anna P.", phone = "", email = "" });
        Assert.Equal("Text", (await Json(other)).GetProperty("receiptOpenIn").GetString());
        _h.F.Clear();
        Assert.Equal(ReceiptOpenIn.Text, _h.F.Db.Users.Single(u => u.Id == _anna.Id).ReceiptOpenIn);

        var back = await _h.Send(HttpMethod.Put, "/api/v1/profile", jwt, new { displayName = "Anna", receiptOpenIn = "Form" });
        Assert.Equal("Form", (await Json(back)).GetProperty("receiptOpenIn").GetString());

        var bad = await _h.Send(HttpMethod.Put, "/api/v1/profile", jwt, new { displayName = "Anna", receiptOpenIn = 7 });
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
    }
}
