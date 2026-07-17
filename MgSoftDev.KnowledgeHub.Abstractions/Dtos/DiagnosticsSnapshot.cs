namespace MgSoftDev.KnowledgeHub.Dtos;

/// <summary>
/// Per-navigation performance metrics captured while opening a page, plus the running
/// cumulative cache counters. Surfaced by the diagnostics panel.
/// </summary>
public sealed class DiagnosticsSnapshot
{
    public string PageTitle { get; set; } = string.Empty;

    // Per-query timings (milliseconds).
    public double HtmlQueryMs { get; set; }
    public double HashesQueryMs { get; set; }
    public double BlobsQueryMs { get; set; }
    public double TotalMs { get; set; }

    // Image accounting for this navigation.
    public int ImageCount { get; set; }
    public int CacheHits { get; set; }
    public int CacheMisses { get; set; }

    /// <summary>Bytes of image binary actually pulled from the store on this navigation.</summary>
    public long BytesFromStore { get; set; }

    // Cumulative counters across the whole session.
    public long CumulativeHits { get; set; }
    public long CumulativeMisses { get; set; }
}
