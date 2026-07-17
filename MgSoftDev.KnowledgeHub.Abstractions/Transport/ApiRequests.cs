using MgSoftDev.KnowledgeHub.Security;

namespace MgSoftDev.KnowledgeHub.Transport;

// Request/response payloads of the KnowledgeHub HTTP API, shared by Http.Server and Http.Client.

public sealed record CreatePageRequest(Guid? ParentPk, string Title, string Slug);

public sealed record RenamePageRequest(string Title);

public sealed record MovePageRequest(Guid? NewParentPk);

public sealed record ReorderPageRequest(int SortOrder);

public sealed record PublishPageRequest(int BaseVersionNumber);

public sealed record SetPermissionsRequest(bool IsPublic, List<string> Permissions);

public sealed record UploadImageRequest(string FileName, string ContentBase64);

public sealed record PrepareHtmlRequest(string StoredHtml);

/// <summary>Payload of GET /me: lets a remote client populate its local user context.</summary>
public sealed class MeResponse
{
    public bool IsAuthenticated { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public List<string> Permissions { get; set; } = new();
    public List<PermissionInfo> Catalog { get; set; } = new();
}
