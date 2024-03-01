namespace DebtBot.Telegram;

public class TelegramLoggerProvider : ILoggerProvider
{
    private readonly string _botToken;
    private readonly long _chatId;
    private readonly IConfigurationSection _logLevelConfig;

    public TelegramLoggerProvider(string botToken, long chatId, IConfigurationSection logLevelConfig)
    {
        _botToken = botToken;
        _chatId = chatId;
        _logLevelConfig = logLevelConfig;
    }

    public ILogger CreateLogger(string categoryName)
    {
        var logLevel = _logLevelConfig[categoryName] ?? _logLevelConfig["Default"];
        return new TelegramLogger(_botToken, _chatId, ParseLogLevel(logLevel));
    }

    public void Dispose()
    {
    }

    private LogLevel ParseLogLevel(string? logLevel)
    {
        return Enum.TryParse(logLevel, out LogLevel parsedLogLevel) ? parsedLogLevel : LogLevel.None;
    }
}

