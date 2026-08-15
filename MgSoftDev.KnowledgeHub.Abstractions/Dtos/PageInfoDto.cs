namespace MgSoftDev.KnowledgeHub.Dtos;

/// <summary>Structural metadata of a page, for the management view.</summary>
public sealed class PageInfoDto
{
    public Guid Pk { get; set; }
    public string Title { get; set; } = null!;
    public string Slug { get; set; } = null!;
    public Guid? Fk_DocPageParent { get; set; }
    public int SortOrder { get; set; }

    /// <summary>Optional Material Symbols icon name shown next to the title.</summary>
    public string? Icon { get; set; }

    /// <summary>Optional CSS color for the icon.</summary>
    public string? IconColor { get; set; }

    /// <summary>
    /// When true the page is skipped by PDF exports. Meant for pages that only exist to navigate —
    /// a board of links — which on paper would be a page of dead references.
    /// </summary>
    public bool ExcludeFromPdf { get; set; }

    /// <summary>When true, the page's <c>{{ … }}</c> placeholders are filled with live data.</summary>
    public bool UsesTemplates { get; set; }
}
