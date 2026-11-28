namespace DebtBot
{
    public class DebtBotConfiguration
    {
        public const string SectionName = "DebtBot";
        public RetryConfiguration Migration { get; set; }
        public ProcessorConfiguration LedgerProcessor { get; set; }
        public ProcessorConfiguration BillProcessor { get; set; }
        public JwtConfiguration JwtConfiguration { get; set; }
        public TelegramConfiguration Telegram { get; set; }
        public ForwardedHeadersConfiguration ForwardedHeaders { get; set; } = new();
        public ApiTokenConfiguration ApiTokens { get; set; } = new();
        public LlmConfiguration Llm { get; set; } = new();
        /// <summary>
        /// The site's public URL (for example <c>https://debts.example.com</c>), for the bot's "Open in the web form"
        /// button. Empty leaves the button out.
        /// </summary>
        public string WebUrl { get; set; } = "";
    }

    /// <summary>
    /// Receipt reading through an OpenAI-compatible chat API (OpenRouter by default). The feature is on only when
    /// <see cref="ApiKey"/> is set, or with the <c>Fake</c> provider in Development.
    /// </summary>
    /// <remarks>
    /// Free models checked on 2026-11-27 (OpenRouter <c>/api/v1/models</c>, image input and <c>:free</c>):
    /// <c>nvidia/nemotron-3.5-super-vl:free</c> (structured outputs), <c>particle-ai/particle-1:free</c> (structured
    /// outputs, new in October 2026), <c>google/gemma-4-31b-it:free</c> and <c>google/gemma-4-26b-a4b-it:free</c>
    /// (<c>response_format</c> only, so <c>json_object</c>), and the <c>openrouter/free</c> router.
    /// <c>inclusionai/ling-3.2-flash</c> no longer has a free variant. Free limits on that date: 20 requests a
    /// minute, and 50 a day (under $10 of credits ever bought) or 1000 a day (at least $10).
    /// </remarks>
    public class LlmConfiguration
    {
        public const string OpenRouterProvider = "OpenRouter";
        public const string FakeProvider = "Fake";

        /// <summary><c>OpenRouter</c> (any OpenAI-compatible API), or <c>Fake</c> (fixtures, Development only).</summary>
        public string Provider { get; set; } = OpenRouterProvider;
        /// <summary>The API base; <c>/chat/completions</c> is added. A local Ollama or vLLM works too.</summary>
        public string BaseUrl { get; set; } = "https://openrouter.ai/api/v1";
        /// <summary>Must take images and, ideally, structured output (see the remarks for the 2026-11-27 list).</summary>
        public string Model { get; set; } = "nvidia/nemotron-3.5-super-vl:free";
        /// <summary>Comma-separated, sent after <see cref="Model"/> as OpenRouter's <c>models</c> fallback list.</summary>
        public string? FallbackModels { get; set; } = "google/gemma-4-31b-it:free";
        /// <summary>Secret: an environment variable (<c>DebtBot_DebtBot__Llm__ApiKey</c>) or user secrets, never appsettings. Empty turns the feature off.</summary>
        public string? ApiKey { get; set; }
        /// <summary><c>json_schema</c> (strict structured output) or <c>json_object</c>, for APIs and models without structured output. The schema is in the prompt either way.</summary>
        public string ResponseFormat { get; set; } = JsonSchemaFormat;
        /// <summary>Optional <c>HTTP-Referer</c> header (OpenRouter shows it as the app's site).</summary>
        public string? Referer { get; set; }
        public int TimeoutSeconds { get; set; } = 45;
        /// <summary>Receipts a user may parse in a rolling hour (web, API token and bot together).</summary>
        public int UserHourlyLimit { get; set; } = 10;
        /// <summary>Receipts a user may parse in a rolling day.</summary>
        public int UserDailyLimit { get; set; } = 30;
        /// <summary>The largest image accepted, in bytes.</summary>
        public int MaxImageBytes { get; set; } = 5 * 1024 * 1024;

        public const string JsonSchemaFormat = "json_schema";
        public const string JsonObjectFormat = "json_object";

        public bool IsFake => string.Equals(Provider, FakeProvider, StringComparison.OrdinalIgnoreCase);

        /// <summary>[primary, ...fallbacks], without blanks or repeats.</summary>
        public List<string> AllModels() =>
            new[] { Model }
                .Concat((FallbackModels ?? "").Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                .Where(m => !string.IsNullOrWhiteSpace(m))
                .Distinct(StringComparer.Ordinal)
                .ToList();
    }

    /// <summary>Personal API tokens (<c>dbt_…</c>).</summary>
    public class ApiTokenConfiguration
    {
        /// <summary>Requests a minute per token (fixed window). 0 or less turns the limit off.</summary>
        public int RateLimitPerMinute { get; set; } = 120;
        /// <summary>Active (not expired, not revoked) tokens a user may have.</summary>
        public int MaxActivePerUser { get; set; } = 20;
    }

    /// <summary>
    /// Behind a reverse proxy: take the client IP and scheme from X-Forwarded-For / X-Forwarded-Proto.
    /// Off by default, because a client that reaches the app directly could then fake its IP.
    /// </summary>
    public class ForwardedHeadersConfiguration
    {
        public bool Enabled { get; set; }
        /// <summary>Comma-separated proxy IPs to trust. With neither this nor <see cref="KnownNetworks"/>, any proxy is trusted.</summary>
        public string? KnownProxies { get; set; }
        /// <summary>Comma-separated CIDR networks to trust, e.g. "172.16.0.0/12".</summary>
        public string? KnownNetworks { get; set; }
    }

    public class RetryConfiguration
    {
        public int RetryCount { get; set; }
        public int RetryDelay { get; set; }
    }

    public class ProcessorConfiguration : RetryConfiguration
    {
        public int ProcessorDelay { get; set; }
    }

    public class JwtConfiguration
    {
        public string Issuer { get; set; }
        public string Audience { get; set; }
        public string Key { get; set; }
        public char[] Secret { get; internal set; }
        public int LifeTime { get; set; }
        /// <summary>Lifetime of web sign-in refresh tokens, in days. Defaults to 30.</summary>
        public int RefreshTokenLifetimeDays { get; set; } = 30;
    }

    public class TelegramConfiguration
    {
        public string BotToken { get; set; }
        public int CountPerPage { get; set; }
        public string LogBotToken { get; set; }
        // Nullable so an empty DebtBot_DebtBot__Telegram__LogChatId= (as in docker-compose.yml) binds as "not set".
        public long? LogChatId { get; set; }

        public IConfigurationSection LogLevel { get; set; }

        /// <summary>The placeholder token shipped in appsettings.json.</summary>
        public const string PlaceholderBotToken = "YOUR:TOKEN:HERE";

        /// <summary>False when no real bot token is configured: the bot doesn't poll and the web UI hides the Telegram widget.</summary>
        public bool IsBotConfigured => IsRealBotToken(BotToken);

        public static bool IsRealBotToken(string? token) =>
            !string.IsNullOrWhiteSpace(token) && token != PlaceholderBotToken;
    }
}
