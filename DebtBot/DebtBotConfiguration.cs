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
