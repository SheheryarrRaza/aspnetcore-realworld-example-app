using System;
using System.Collections.Generic;
using System.Threading;

namespace Conduit.Features.Articles;

/// <summary>
/// Holds per-article comment counts for list responses for a short time.
/// Comment create and delete evict the affected article, so the count is never stale for them.
/// </summary>
public sealed class CommentCountCache(TimeProvider timeProvider)
{
    public static readonly TimeSpan TimeToLive = TimeSpan.FromSeconds(30);
    public const int MaxEntries = 10_000;

    private readonly Lock _lock = new();
    private readonly Dictionary<int, Entry> _entries = [];
    private long _generation;

    /// <summary>
    /// Returns the generation to pass to <see cref="Store"/>. Read it before querying the database,
    /// so counts read before an eviction are not cached after it.
    /// </summary>
    public long Snapshot()
    {
        lock (_lock)
        {
            return _generation;
        }
    }

    public bool TryGet(int articleId, out int count)
    {
        lock (_lock)
        {
            if (
                _entries.TryGetValue(articleId, out var entry)
                && entry.ExpiresAt > timeProvider.GetUtcNow()
            )
            {
                count = entry.Count;
                return true;
            }

            count = 0;
            return false;
        }
    }

    public void Store(IReadOnlyDictionary<int, int> counts, long snapshot)
    {
        lock (_lock)
        {
            if (snapshot != _generation)
            {
                return;
            }

            var now = timeProvider.GetUtcNow();
            foreach (var (articleId, count) in counts)
            {
                if (_entries.Count >= MaxEntries)
                {
                    RemoveExpired(now);
                    if (_entries.Count >= MaxEntries)
                    {
                        return;
                    }
                }

                _entries[articleId] = new Entry(count, now + TimeToLive);
            }
        }
    }

    public void Invalidate(int articleId)
    {
        lock (_lock)
        {
            _generation++;
            _entries.Remove(articleId);
        }
    }

    private void RemoveExpired(DateTimeOffset now)
    {
        foreach (var (articleId, entry) in new List<KeyValuePair<int, Entry>>(_entries))
        {
            if (entry.ExpiresAt <= now)
            {
                _entries.Remove(articleId);
            }
        }
    }

    private readonly record struct Entry(int Count, DateTimeOffset ExpiresAt);
}
