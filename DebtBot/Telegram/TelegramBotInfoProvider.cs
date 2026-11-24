using Microsoft.Extensions.Options;
using Telegram.Bot;

namespace DebtBot.Telegram;

/// <summary>
/// The bot's username for the Telegram Login Widget. Asked from Telegram once and cached;
/// after a failed call it's retried at most every few minutes.
/// </summary>
public class TelegramBotInfoProvider
{
    private static readonly TimeSpan RetryDelay = TimeSpan.FromMinutes(5);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TelegramConfiguration? _telegramConfig;
    private readonly ILogger<TelegramBotInfoProvider> _logger;
    private readonly SemaphoreSlim _lock = new(1, 1);

    private string? _botUsername;
    private DateTime _retryAfter = DateTime.MinValue;

    public TelegramBotInfoProvider(IServiceScopeFactory scopeFactory, IOptions<DebtBotConfiguration> config, ILogger<TelegramBotInfoProvider> logger)
    {
        _scopeFactory = scopeFactory;
        _telegramConfig = config.Value.Telegram;
        _logger = logger;
    }

    public async Task<string?> GetBotUsernameAsync(CancellationToken cancellationToken = default)
    {
        if (_telegramConfig?.IsBotConfigured != true)
        {
            return null;
        }
        if (_botUsername is not null)
        {
            return _botUsername;
        }
        if (DateTime.UtcNow < _retryAfter)
        {
            return null;
        }

        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (_botUsername is not null || DateTime.UtcNow < _retryAfter)
            {
                return _botUsername;
            }

            using var scope = _scopeFactory.CreateScope();
            var botClient = scope.ServiceProvider.GetRequiredService<ITelegramBotClient>();
            var me = await botClient.GetMe(cancellationToken);
            _botUsername = me.Username;
            return _botUsername;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Could not get the bot's username from Telegram; the Telegram sign-in widget stays hidden for now");
            _retryAfter = DateTime.UtcNow.Add(RetryDelay);
            return null;
        }
        finally
        {
            _lock.Release();
        }
    }
}
