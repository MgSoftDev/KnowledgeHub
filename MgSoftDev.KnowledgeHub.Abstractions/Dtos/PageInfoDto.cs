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
}
