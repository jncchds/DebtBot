using Microsoft.Extensions.Options;
using System.Collections.Concurrent;

namespace DebtBot.Receipts;

public enum ReceiptLimitWindow
{
    Hour,
    Day,
}

public readonly record struct ReceiptLimitDecision(bool Allowed, ReceiptLimitWindow? Window = null, TimeSpan? RetryAfter = null);

/// <summary>
/// Receipts per user in a rolling hour and day (<c>Llm:UserHourlyLimit</c>, <c>Llm:UserDailyLimit</c>), the same
/// whether a call comes from the web, an API token or the bot. A service, not endpoint middleware, so the bot's
/// handler goes through it too (it's called by <see cref="IReceiptService.ParseAsync"/>). In memory: a restart
/// resets the counts.
/// </summary>
public interface IReceiptRateLimiter
{
    /// <summary>Takes one use, or says which limit is reached and when the next use is free.</summary>
    ReceiptLimitDecision TryAcquire(Guid userId);
}

public sealed class ReceiptRateLimiter : IReceiptRateLimiter
{
    private static readonly TimeSpan Hour = TimeSpan.FromHours(1);
    private static readonly TimeSpan Day = TimeSpan.FromDays(1);

    private readonly ConcurrentDictionary<Guid, Queue<DateTimeOffset>> _uses = new();
    private readonly TimeProvider _time;
    private readonly IOptions<DebtBotConfiguration> _options;

    public ReceiptRateLimiter(TimeProvider time, IOptions<DebtBotConfiguration> options)
    {
        _time = time;
        _options = options;
    }

    public ReceiptLimitDecision TryAcquire(Guid userId)
    {
        var settings = _options.Value.Llm;
        var now = _time.GetUtcNow();
        var uses = _uses.GetOrAdd(userId, _ => new Queue<DateTimeOffset>());

        lock (uses)
        {
            while (uses.Count > 0 && uses.Peek() <= now - Day)
            {
                uses.Dequeue();
            }

            if (settings.UserDailyLimit > 0 && uses.Count >= settings.UserDailyLimit)
            {
                // The oldest of the last N uses has to leave the window.
                var oldest = uses.ElementAt(uses.Count - settings.UserDailyLimit);
                return new(false, ReceiptLimitWindow.Day, oldest + Day - now);
            }

            var lastHour = uses.Where(u => u > now - Hour).ToList();
            if (settings.UserHourlyLimit > 0 && lastHour.Count >= settings.UserHourlyLimit)
            {
                var oldest = lastHour[lastHour.Count - settings.UserHourlyLimit];
                return new(false, ReceiptLimitWindow.Hour, oldest + Hour - now);
            }

            uses.Enqueue(now);
            return new(true);
        }
    }
}
