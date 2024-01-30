namespace DebtBot
{
    public class DebtBotConfiguration
    {
        public Retries Migration { get; set; }
        public Retries LedgerProcessor { get; set; }
        public Retries BillProcessor { get; set; }
    }

    public class Retries
    {
        public int RetryCount { get; set; }
        public int RetryDelay { get; set; }
    }
}
