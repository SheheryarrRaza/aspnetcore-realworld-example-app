using System;
using Microsoft.Extensions.Caching.Memory;

namespace Conduit.Features.Profiles;

/// <summary>
/// Private in-process cache of the favourite count per person, kept for 60 seconds.
/// Entries are keyed by person id so a renamed or reused username never serves another person's count.
/// </summary>
public sealed class ProfileCountsCache : IDisposable
{
    private const int MaxEntries = 10_000;

    private static readonly TimeSpan s_lifetime = TimeSpan.FromSeconds(60);

    private readonly MemoryCache m_cache = new(new MemoryCacheOptions { SizeLimit = MaxEntries });

    /// <summary>
    /// Returns the cached favourite count of a person, or null when none is cached.
    /// </summary>
    public int? GetFavoritesCount(int personId) =>
        m_cache.TryGetValue(personId, out int count) ? count : null;

    /// <summary>
    /// Caches the favourite count of a person for 60 seconds.
    /// </summary>
    public void SetFavoritesCount(int personId, int count) =>
        m_cache.Set(
            personId,
            count,
            new MemoryCacheEntryOptions { Size = 1, AbsoluteExpirationRelativeToNow = s_lifetime }
        );

    /// <summary>
    /// Drops the cached counts of a person so the next read queries the database.
    /// </summary>
    public void Evict(int personId) => m_cache.Remove(personId);

    /// <summary>
    /// Releases the underlying cache.
    /// </summary>
    public void Dispose() => m_cache.Dispose();
}
