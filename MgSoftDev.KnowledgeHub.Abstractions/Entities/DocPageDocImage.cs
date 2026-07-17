namespace MgSoftDev.KnowledgeHub.Entities;

/// <summary>
/// Many-to-many join recording which images a page references (table DocPages_DocImages).
/// Recalculated on every save by parsing the page HTML; lets us detect orphan images
/// without re-parsing HTML on each read.
/// </summary>
public class DocPageDocImage : EntityBase
{
    public Guid Fk_DocPage { get; set; }
    public Guid Fk_DocImage { get; set; }

    public DocPage DocPage { get; set; } = null!;
    public DocImage DocImage { get; set; } = null!;
}
