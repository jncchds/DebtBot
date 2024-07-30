using System.Threading.RateLimiting;

namespace DebtBot.Telegram;

public class TelegramRateLimiter
{
    private readonly RateLimiter _limiter;

    public TelegramRateLimiter()
    {
        _limiter = new SlidingWindowRateLimiter(new SlidingWindowRateLimiterOptions
        {
            AutoReplenishment = true,
            PermitLimit = 30,
            Window = TimeSpan.FromSeconds(1),
            QueueLimit = 1000,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            SegmentsPerWindow = 30
        });
    }

    public async Task ProcessAsync(Func<Task> action)
    {
        var lease = await _limiter.AcquireAsync();
        if (lease.IsAcquired)
        {
            await action();
        }
        else
        {
            Console.WriteLine("Queue was not enough");
        }
    }
}
