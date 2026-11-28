using DebtBot.Models.Receipt;
using Microsoft.Extensions.Caching.Memory;

namespace DebtBot.Receipts;

/// <summary>
/// Parsed receipts (never images), kept 15 minutes for their owner only. In memory: a draft is short-lived, holds
/// nothing that can't be made again, and is read by the same process (the web prefill and the bot's button), so a
/// table would only add a migration and a purge job. A restart forgets them, which is fine for 15 minutes.
/// </summary>
public interface IReceiptDraftStore
{
    /// <summary>Gives the draft an id and expiry, and keeps it.</summary>
    ReceiptDraft Add(Guid ownerId, ReceiptDraft draft);
    /// <summary>The draft, or null when it's someone else's, unknown or expired.</summary>
    ReceiptDraft? Get(Guid ownerId, Guid id);
}

public sealed class ReceiptDraftStore : IReceiptDraftStore
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(15);

    private readonly IMemoryCache _cache;
    private readonly TimeProvider _time;

    public ReceiptDraftStore(IMemoryCache cache, TimeProvider time)
    {
        _cache = cache;
        _time = time;
    }

    private sealed record Entry(Guid OwnerId, ReceiptDraft Draft);

    private static string Key(Guid id) => "receipt-draft:" + id.ToString("N");

    public ReceiptDraft Add(Guid ownerId, ReceiptDraft draft)
    {
        draft.Id = Guid.NewGuid();
        draft.ExpiresAt = _time.GetUtcNow().UtcDateTime + Lifetime;
        // The cache's own expiry frees the memory; the check in Get (on our clock) decides.
        _cache.Set(Key(draft.Id), new Entry(ownerId, draft), new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = Lifetime,
            Size = 1,
        });
        return draft;
    }

    public ReceiptDraft? Get(Guid ownerId, Guid id)
    {
        if (!_cache.TryGetValue(Key(id), out Entry? entry) || entry is null || entry.OwnerId != ownerId)
        {
            return null;
        }

        if (_time.GetUtcNow().UtcDateTime >= entry.Draft.ExpiresAt)
        {
            _cache.Remove(Key(id));
            return null;
        }

        return entry.Draft;
    }
}
