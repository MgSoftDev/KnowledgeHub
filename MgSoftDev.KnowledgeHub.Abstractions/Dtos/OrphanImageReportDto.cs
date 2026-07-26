namespace MgSoftDev.KnowledgeHub.Dtos;

/// <summary>
/// Result of scanning the image store for content no page references any more.
///
/// An image counts as an orphan only when NO version references it — the whole history included.
/// The page↔image link table is NOT a valid source for this: it is replaced with the images of the
/// latest saved version of each page, so an image used only by an older version already has no
/// links and deleting it would break the history and any restore of that version.
/// </summary>
public sealed class OrphanImageReportDto
{
    /// <summary>Images currently stored.</summary>
    public int TotalImages { get; set; }

    /// <summary>Images referenced by at least one version (any page, any version).</summary>
    public int ReferencedImages { get; set; }

    /// <summary>Images no version references any more; these are the deletion candidates.</summary>
    public int OrphanImages { get; set; }

    /// <summary>Bytes that deleting the orphans would free (sum of their SizeBytes).</summary>
    public long OrphanBytes { get; set; }

    /// <summary>A few orphan file names, so the user can sanity-check before deleting.</summary>
    public List<string> SampleFileNames { get; set; } = new();
}
