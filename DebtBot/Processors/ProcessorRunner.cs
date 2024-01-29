using DebtBot.Interfaces;

namespace DebtBot.Processors
{
    public class ProcessorRunner : IHostedService
    {
        private readonly ILogger<ProcessorRunner> _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly IServiceScope _serviceScope;

        public ProcessorRunner(ILogger<ProcessorRunner> logger, IServiceProvider serviceProvider)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
            _serviceScope = _serviceProvider.CreateScope();
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            var processors = _serviceScope.ServiceProvider.GetRequiredService<IEnumerable<IProcessor>>();
            foreach (var processor in processors)
            {
                _ = Task.Run(async () =>
                {
                    while (true)
                    {
                        try
                        {
                            _logger.LogInformation("Running processor {Processor}", processor.GetType().Name);
                            await processor.Run(cancellationToken);
                        }
                        catch (Exception e)
                        {
                            _logger.LogError(e, "Error while running processor {Processor}", processor.GetType().Name);
                        }

                        if (cancellationToken.IsCancellationRequested)
                        {
                            break;
                        }

                        await Task.Delay(processor.Delay, cancellationToken);

                        if (cancellationToken.IsCancellationRequested)
                        {
                            break;
                        }
                    }
                }, cancellationToken);
            }
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _serviceScope.Dispose();
            return Task.CompletedTask;
        }
    }
}
