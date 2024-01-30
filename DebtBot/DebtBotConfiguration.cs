namespace DebtBot
{
    public class DebtBotConfiguration
    {
        public RetryConfiguration Migration { get; set; }
        public ProcessorConfiguration LedgerProcessor { get; set; }
        public ProcessorConfiguration BillProcessor { get; set; }
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
}
