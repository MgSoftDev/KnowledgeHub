namespace MgSoftDev.KnowledgeHub.Dtos;

/// <summary>
/// Everything a renderer needs to produce the file, and nothing about how to produce it. The core
/// builds it after resolving permissions and visibility, so a renderer never has to think about
/// security: whatever reaches it is already cleared for that user.
/// </summary>
public sealed class PdfExportDocument
{
    /// <summary>Title of the export — the root page's title. Used for the cover and the file name.</summary>
    public string Title { get; set; } = null!;

    public DateTime GeneratedAt { get; set; }

    /// <summary>Display name of whoever asked for it, for the cover. Null when the host has no name.</summary>
    public string? GeneratedBy { get; set; }

    /// <summary>The pages, already in tree order (parents before their children).</summary>
    public List<PdfExportSection> Sections { get; set; } = new();

    /// <summary>
    /// Every image referenced by the sections, keyed by the pk that appears in their
    /// <c>docimg://</c> references. Kept apart from the HTML on purpose: inlining them as data
    /// URIs would multiply the content in memory for a branch of any size.
    /// </summary>
    public Dictionary<Guid, PdfExportImage> Images { get; set; } = new();
}

/// <summary>One exported page.</summary>
public sealed class PdfExportSection
{
    public Guid PagePk { get; set; }

    public string Title { get; set; } = null!;

    /// <summary>
    /// Depth relative to the root of the export: 1 for the root page, 2 for its children, and so
    /// on. Drives heading sizes and the PDF outline, so it is NOT the depth in the whole tree.
    /// </summary>
    public int Level { get; set; }

    /// <summary>
    /// The published HTML, with its <c>docimg://{pk}</c> references INTACT. Resolving them is the
    /// renderer's job, against <see cref="PdfExportDocument.Images"/>.
    /// </summary>
    public string ContentHtml { get; set; } = null!;

    public int VersionNumber { get; set; }

    public DateTime? PublishedAt { get; set; }
}

/// <summary>An image, exactly as it is stored (WebP) — transcoding is up to the renderer.</summary>
public sealed class PdfExportImage
{
    public Guid Pk { get; set; }

    /// <summary>Stored media type, in practice <c>image/webp</c>.</summary>
    public string ContentType { get; set; } = null!;

    public byte[] Content { get; set; } = Array.Empty<byte>();

    public int Width { get; set; }

    public int Height { get; set; }
}

/// <summary>The finished file.</summary>
public sealed class PdfFileDto
{
    public string FileName { get; set; } = null!;

    public string ContentType { get; set; } = "application/pdf";

    public byte[] Content { get; set; } = Array.Empty<byte>();
}
