using MgSoftDev.KnowledgeHub.Enums;

namespace MgSoftDev.KnowledgeHub.Dtos;

/// <summary>One row of the version-history grid.</summary>
public sealed class VersionListItemDto
{
    public Guid Pk { get; set; }
    public int VersionNumber { get; set; }

    /// <summary>UserName that created the version (taken directly from RowUserCreate).</summary>
    public string? AuthorName { get; set; }

    public DateTime RowCreateDate { get; set; }
    public DocPageStatus Status { get; set; }
    public string? ChangeNote { get; set; }
    public DateTime? PublishedAt { get; set; }

    /// <summary>True when this version is the one currently published for the page.</summary>
    public bool IsCurrentPublished { get; set; }
}
