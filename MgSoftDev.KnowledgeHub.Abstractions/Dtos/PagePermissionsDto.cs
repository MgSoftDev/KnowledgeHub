namespace MgSoftDev.KnowledgeHub.Dtos;

/// <summary>Visibility configuration of a page: public flag plus the host permissions allowed to view it.</summary>
public sealed class PagePermissionsDto
{
    public bool IsPublic { get; set; }
    public List<string> Permissions { get; set; } = new();
}
