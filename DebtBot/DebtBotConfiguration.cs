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
    }

    public class TelegramConfiguration
    {
        public string BotToken { get; set; }
        public int CountPerPage { get; set; }
    }
}
