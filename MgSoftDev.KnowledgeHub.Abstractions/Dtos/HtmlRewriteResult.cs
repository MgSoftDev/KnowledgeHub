namespace MgSoftDev.KnowledgeHub.Dtos;

/// <summary>
/// Outcome of preparing a page's images for display: the HTML with its <c>docimg://</c>
/// sources rewritten to display URLs, plus the measurements the diagnostics panel reports.
/// </summary>
public sealed class HtmlRewriteResult
{
    public string Html { get; set; } = string.Empty;

    /// <summary>Time of the query that fetched Pk + ContentHash for the referenced images.</summary>
    public double HashesQueryMs { get; set; }

    /// <summary>Time of the single batch query that fetched the missing image binaries (0 if none).</summary>
    public double BlobsQueryMs { get; set; }

    public int ImageCount { get; set; }
    public int CacheHits { get; set; }
    public int CacheMisses { get; set; }

    /// <summary>Bytes of image binary pulled from the store on this navigation.</summary>
    public long BytesFromStore { get; set; }
}
