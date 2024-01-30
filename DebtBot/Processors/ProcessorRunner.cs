using DebtBot.Interfaces;

namespace DebtBot.Processors
{
    public class ProcessorRunner<T> : BackgroundService
        where T : IProcessor
    {
        private readonly ILogger<ProcessorRunner<T>> _logger;
        private readonly IServiceProvider _serviceProvider;
        

        public ProcessorRunner(ILogger<ProcessorRunner<T>> logger, IServiceProvider serviceProvider)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
        }

        protected override async Task ExecuteAsync(CancellationToken token)
        {
            await DoWork(token);
        }

        private async Task DoWork(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                using (var scope = _serviceProvider.CreateScope())
                {
                    IProcessor processor = scope.ServiceProvider.GetRequiredService<T>();
                    await processor.Run(token);
                    await Task.Delay(processor.Delay, token);
                }
            }
        }
    }
}
