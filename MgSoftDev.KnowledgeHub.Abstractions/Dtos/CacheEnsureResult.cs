namespace MgSoftDev.KnowledgeHub.Dtos;

/// <summary>Metrics of one cache-ensure pass (see IKnowledgeHubImageCache.EnsureCachedAsync).</summary>
public sealed class CacheEnsureResult
{
    public int CacheHits { get; set; }
    public int CacheMisses { get; set; }

    /// <summary>Time of the single batch query that fetched the missing binaries (0 if none).</summary>
    public double BlobsQueryMs { get; set; }

    /// <summary>Bytes of image binary pulled from the store by this pass.</summary>
    public long BytesFromStore { get; set; }
}
