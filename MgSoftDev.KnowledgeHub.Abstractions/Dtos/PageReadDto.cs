using MgSoftDev.KnowledgeHub.Enums;

namespace MgSoftDev.KnowledgeHub.Dtos;

/// <summary>
/// Content payload for the reader (or for viewing a specific historical version).
/// <see cref="ContentHtml"/> still holds the stable docimg://{pk} references; the HTML image
/// rewriter turns them into display URLs before rendering.
/// </summary>
public sealed class PageReadDto
{
    public Guid PagePk { get; set; }
    public Guid VersionPk { get; set; }
    public string Title { get; set; } = null!;
    public string ContentHtml { get; set; } = null!;
    public int VersionNumber { get; set; }
    public DocPageStatus Status { get; set; }
    public DateTime? PublishedAt { get; set; }

    /// <summary>Optional Material Symbols icon name of the page (null when viewing history).</summary>
    public string? Icon { get; set; }

    /// <summary>Optional CSS color for the icon.</summary>
    public string? IconColor { get; set; }
}
