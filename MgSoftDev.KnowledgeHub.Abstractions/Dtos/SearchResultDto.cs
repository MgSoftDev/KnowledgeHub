namespace MgSoftDev.KnowledgeHub.Dtos;

/// <summary>A search hit over published pages, already filtered by permissions.</summary>
public sealed class SearchResultDto
{
    public Guid PagePk { get; set; }
    public string Title { get; set; } = null!;
    public string Slug { get; set; } = null!;

    /// <summary>Short plain-text excerpt around the match.</summary>
    public string Snippet { get; set; } = string.Empty;

    /// <summary>Optional Material Symbols icon name of the page.</summary>
    public string? Icon { get; set; }

    /// <summary>Optional CSS color for the icon.</summary>
    public string? IconColor { get; set; }
}
