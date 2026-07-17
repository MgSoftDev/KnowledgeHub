using MgSoftDev.KnowledgeHub.Enums;

namespace MgSoftDev.KnowledgeHub.Entities;

/// <summary>
/// One immutable snapshot of a page's content. Every save inserts a new row; the content
/// of an existing version is never updated.
/// </summary>
public class DocPageVersion : EntityBase
{
    public Guid Fk_DocPage { get; set; }

    /// <summary>Sequential number per page, starting at 1.</summary>
    public int VersionNumber { get; set; }

    /// <summary>The title is versioned together with the content.</summary>
    public string Title { get; set; } = null!;

    /// <summary>Page HTML holding stable <c>docimg://{pk}</c> image references.</summary>
    public string ContentHtml { get; set; } = null!;

    public DocPageStatus Status { get; set; }

    /// <summary>Optional editor comment describing the change.</summary>
    public string? ChangeNote { get; set; }

    public DateTime? PublishedAt { get; set; }

    public DocPage DocPage { get; set; } = null!;
}
