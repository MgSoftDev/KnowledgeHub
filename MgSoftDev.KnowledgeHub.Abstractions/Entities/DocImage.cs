namespace MgSoftDev.KnowledgeHub.Entities;

/// <summary>
/// Image metadata only. The binary never lives here so this table can be queried cheaply.
/// The binary is stored 1:1 in <see cref="DocImageContent"/>.
/// </summary>
public class DocImage : EntityBase
{
    public string FileName { get; set; } = null!;

    /// <summary>Lower-case hex SHA-256 of the binary. Drives cache file naming and deduplication.</summary>
    public string ContentHash { get; set; } = null!;

    /// <summary>Usually image/webp.</summary>
    public string ContentType { get; set; } = null!;

    public long SizeBytes { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }

    public DocImageContent? Content { get; set; }
    public ICollection<DocPageDocImage> DocPageDocImages { get; set; } = new List<DocPageDocImage>();
}
