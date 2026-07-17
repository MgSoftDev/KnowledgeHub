using MgSoftDev.KnowledgeHub.Dtos;
using MgSoftDev.ReturningCore;

namespace MgSoftDev.KnowledgeHub.Contracts;

/// <summary>
/// Optional local, hash-addressed image cache. Files are named by their pure content hash
/// (<c>{hash}.webp</c>) so the same image used on many pages occupies a single file, and a
/// replaced image (new binary → new hash → new name) is always re-fetched. There is no
/// time-based expiration. Registered in hosts with local disk (WPF, Blazor Server);
/// absent in WASM clients.
/// </summary>
public interface IKnowledgeHubImageCache
{
    /// <summary>Absolute path of the cache folder (mapped to the WebView virtual host in WPF).</summary>
    string CacheFolder { get; }

    /// <summary>
    /// Ensures every referenced binary is on disk, fetching only the missing ones from the
    /// store in a single batch. Returns hit/miss metrics.
    /// </summary>
    Task<Returning<CacheEnsureResult>> EnsureCachedAsync(IReadOnlyCollection<ImageRefDto> images);

    /// <summary>Deletes every file in the cache folder (forces a cold start).</summary>
    Returning ClearCache();

    /// <summary>Total size, in bytes, of the cache folder.</summary>
    long GetCacheFolderSizeBytes();

    /// <summary>Removes cache files whose hash no longer exists in DocImages. Returns the count removed.</summary>
    Task<Returning<int>> CleanupOrphansAsync();
}
