namespace MgSoftDev.KnowledgeHub.Dtos;

/// <summary>
/// A node of the navigation tree, already filtered by permissions. Children are populated
/// in memory when the core service assembles the tree (stores return a flat list).
/// </summary>
public sealed class PageTreeNodeDto
{
    public Guid Pk { get; set; }
    public Guid? Fk_DocPageParent { get; set; }
    public string Title { get; set; } = null!;
    public string Slug { get; set; } = null!;
    public int SortOrder { get; set; }
    public bool IsPublic { get; set; }

    /// <summary>True when the page has a published version (readers can open it).</summary>
    public bool HasPublishedVersion { get; set; }

    public List<PageTreeNodeDto> Children { get; set; } = new();
}
