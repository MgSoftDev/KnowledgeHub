namespace MgSoftDev.KnowledgeHub.Entities;

/// <summary>
/// A documentation page: a node in the hierarchical tree plus a pointer to the version
/// that is currently published (visible to readers).
/// </summary>
public class DocPage : EntityBase
{
    /// <summary>Parent page (self reference). NULL for a root page.</summary>
    public Guid? Fk_DocPageParent { get; set; }

    /// <summary>The version currently published. NULL while the page has never been published.</summary>
    public Guid? Fk_DocPageVersionPublished { get; set; }

    public string Slug { get; set; } = null!;
    public string Title { get; set; } = null!;
    public int SortOrder { get; set; }

    /// <summary>When true, the page is visible to everyone and DocPages_Permissions is ignored.</summary>
    public bool IsPublic { get; set; }

    public DocPage? Parent { get; set; }
    public ICollection<DocPage> Children { get; set; } = new List<DocPage>();

    /// <summary>The published version pointed to by <see cref="Fk_DocPageVersionPublished"/>.</summary>
    public DocPageVersion? PublishedVersion { get; set; }

    /// <summary>Full version history of this page.</summary>
    public ICollection<DocPageVersion> Versions { get; set; } = new List<DocPageVersion>();

    public ICollection<DocPagePermission> DocPagePermissions { get; set; } = new List<DocPagePermission>();
    public ICollection<DocPageDocImage> DocPageDocImages { get; set; } = new List<DocPageDocImage>();
}
