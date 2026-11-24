using Microsoft.Extensions.Caching.Memory;

namespace DebtBot.Services;

/// <summary>
/// Counts failed password sign-ins per login (existing or not) and locks that login for a while after too many.
/// Works together with the per-IP rate limiter on the auth endpoints.
/// </summary>
public class LoginThrottle
{
    public const int MaxFailures = 5;
    public static readonly TimeSpan Window = TimeSpan.FromMinutes(15);

    private readonly IMemoryCache _cache;

    public LoginThrottle(IMemoryCache cache)
    {
        _cache = cache;
    }

    public bool IsLockedOut(string login) =>
        _cache.TryGetValue(Key(login), out Counter? counter) && counter!.Count >= MaxFailures;

    public void RecordFailure(string login)
    {
        var counter = _cache.GetOrCreate(Key(login), entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = Window;
            return new Counter();
        })!;
        Interlocked.Increment(ref counter.Count);
    }

    public void Reset(string login) => _cache.Remove(Key(login));

    private static string Key(string login) => $"login-failures:{login}";

    private sealed class Counter
    {
        public int Count;
    }
}
