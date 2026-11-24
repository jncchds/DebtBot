using Microsoft.Extensions.Options;

namespace DebtBot.Telegram;

public class PollingService : PollingServiceBase<ReceiverService>
{
    private readonly bool _botConfigured;
    private readonly ILogger<PollingService> _logger;

    public PollingService(IServiceProvider serviceProvider, ILogger<PollingService> logger, IOptions<DebtBotConfiguration> config)
        : base(serviceProvider, logger)
    {
        _botConfigured = config.Value.Telegram?.IsBotConfigured == true;
        _logger = logger;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_botConfigured)
        {
            _logger.LogWarning("No Telegram bot token is configured (DebtBot:Telegram:BotToken); not polling Telegram");
            return Task.CompletedTask;
        }

        return base.ExecuteAsync(stoppingToken);
    }
}
