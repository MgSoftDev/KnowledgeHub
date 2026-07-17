namespace MgSoftDev.KnowledgeHub.Entities;

/// <summary>
/// The image binary, kept in its own table (1:1 with <see cref="DocImage"/>) so metadata
/// queries never drag megabytes of binary content over the wire.
/// </summary>
public class DocImageContent : EntityBase
{
    public Guid Fk_DocImage { get; set; }

    public byte[] Content { get; set; } = null!;

    public DocImage DocImage { get; set; } = null!;
}
