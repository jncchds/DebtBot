using Polly;
using System.Threading.RateLimiting;

namespace DebtBot.Processors;

public class RateLimittingProcessor
{
    private readonly RateLimiter _limitter;

    public RateLimittingProcessor()
    {
        _limitter = new SlidingWindowRateLimiter(new SlidingWindowRateLimiterOptions
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
        var lease = await _limitter.AcquireAsync();
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
