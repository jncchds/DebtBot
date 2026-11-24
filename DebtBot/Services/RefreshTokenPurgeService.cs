using DebtBot.Interfaces.Services;

namespace DebtBot.Services;

/// <summary>
/// Removes dead refresh tokens (expired, or revoked a while ago) a minute after start and then every 12 hours.
/// One DELETE per run on a small table, so it stays cheap.
/// </summary>
public class RefreshTokenPurgeService : BackgroundService
{
    public static readonly TimeSpan Interval = TimeSpan.FromHours(12);
    private static readonly TimeSpan FirstRunDelay = TimeSpan.FromMinutes(1);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<RefreshTokenPurgeService> _logger;

    public RefreshTokenPurgeService(IServiceScopeFactory scopeFactory, ILogger<RefreshTokenPurgeService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(FirstRunDelay, stoppingToken);
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var deleted = await scope.ServiceProvider.GetRequiredService<IAuthService>().PurgeRefreshTokensAsync(stoppingToken);
                    if (deleted > 0)
                    {
                        _logger.LogInformation("Purged {Count} expired or revoked refresh tokens", deleted);
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogWarning(ex, "Refresh token purge failed; trying again later");
                }

                await Task.Delay(Interval, stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            // shutting down
        }
    }
}
