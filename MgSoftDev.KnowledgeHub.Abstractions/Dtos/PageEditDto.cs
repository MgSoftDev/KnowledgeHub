namespace MgSoftDev.KnowledgeHub.Dtos;

/// <summary>
/// Editable payload for the editor. Bound two-way to the UI, hence mutable.
/// <see cref="BaseVersionNumber"/> is the highest version number the editor saw when the
/// draft was loaded; it is compared against the store on publish to detect a concurrent
/// publish from another machine.
/// </summary>
public sealed class PageEditDto
{
    public Guid PagePk { get; set; }
    public string Slug { get; set; } = null!;
    public string Title { get; set; } = null!;
    public string ContentHtml { get; set; } = string.Empty;
    public string? ChangeNote { get; set; }
    public bool IsPublic { get; set; }

    /// <summary>Version number this draft was branched from (concurrency baseline).</summary>
    public int BaseVersionNumber { get; set; }
}
