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

    /// <summary>Optional Material Symbols icon name of the page (shown next to the title).</summary>
    public string? Icon { get; set; }

    /// <summary>Optional CSS color for the icon.</summary>
    public string? IconColor { get; set; }

    /// <summary>
    /// Whether this page's <c>{{ … }}</c> are filled with live data. Read-only here — it is changed
    /// from the management screen — and the editor uses it to decide whether to check the template
    /// after saving. The content itself always arrives RAW: the editor shows the template, never
    /// its result.
    /// </summary>
    public bool UsesTemplates { get; set; }

    /// <summary>Version number this draft was branched from (concurrency baseline).</summary>
    public int BaseVersionNumber { get; set; }
}
