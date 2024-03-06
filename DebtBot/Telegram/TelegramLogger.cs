using Telegram.Bot;
using Telegram.Bot.Types.Enums;

namespace DebtBot.Telegram;

public class TelegramLogger : ILogger
{
    private readonly TelegramBotClient _botClient;
    private readonly long _chatId;
    private readonly LogLevel _logLevel;

    public TelegramLogger(string botToken, long chatId, LogLevel logLevel)
    {
        _botClient = new TelegramBotClient(botToken);
        _chatId = chatId;
        _logLevel = logLevel;
    }

    public IDisposable BeginScope<TState>(TState state) => null;

    public bool IsEnabled(LogLevel logLevel) => logLevel >= _logLevel;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
            return;

        var message = formatter(state, exception);
        var entry = $"[{DateTime.UtcNow:O}] {logLevel}:\n<b>{message}</b>";

        while (exception != null)
        {
            entry += $"\n\n<b>{exception.Message}</b>\n{exception.StackTrace}";
            exception = exception.InnerException;
        }
        try
        {
            _botClient.SendTextMessageAsync(_chatId, entry, parseMode: ParseMode.Html).Wait();
        }
        catch (Exception e)
        {
            Console.WriteLine($"Error sending log message to Telegram: {e.Message}");
        }
    }
}

