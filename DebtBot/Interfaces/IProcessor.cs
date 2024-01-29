namespace DebtBot.Interfaces
{
    public interface IProcessor
    {
        public Task Run(CancellationToken token);

        public int Delay { get; }
    }
}
