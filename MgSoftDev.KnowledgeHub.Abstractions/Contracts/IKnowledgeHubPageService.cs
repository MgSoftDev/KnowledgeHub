using MgSoftDev.KnowledgeHub.Dtos;
using MgSoftDev.ReturningCore;

namespace MgSoftDev.KnowledgeHub.Contracts;

/// <summary>
/// Documentation pages: tree navigation, reading, editing, versioning, publishing, page
/// management, per-page visibility and search. Visibility is always enforced in the query,
/// never in the UI, and mutations are guarded server-side by the reserved KnowledgeHub.*
/// permissions.
/// </summary>
public interface IKnowledgeHubPageService
{
    /// <summary>Builds the navigation tree the current user is allowed to see (inherited visibility).</summary>
    Task<ReturningList<PageTreeNodeDto>> GetTreeAsync();

    /// <summary>Loads the published version for a reader. Denied if not visible or not published.</summary>
    Task<Returning<PageReadDto>> GetPageForReadAsync(Guid pagePk);

    /// <summary>Loads the latest content into an editable payload (editors only).</summary>
    Task<Returning<PageEditDto>> GetPageForEditAsync(Guid pagePk);

    /// <summary>Loads a specific historical version's content (for view/restore).</summary>
    Task<Returning<PageReadDto>> GetVersionContentAsync(Guid versionPk);

    /// <summary>Inserts a new Draft version (VersionNumber = MAX + 1). Returns the new version number.</summary>
    Task<Returning<int>> SaveDraftAsync(PageEditDto draft);

    /// <summary>
    /// Publishes the latest version atomically, moving the page's published pointer.
    /// Returns Unfinished/Warning if another user published a newer version since
    /// <paramref name="baseVersionNumber"/> was loaded.
    /// </summary>
    Task<Returning> PublishAsync(Guid pagePk, int baseVersionNumber);

    /// <summary>Creates a new Draft version that copies an old version's content (never deletes history).</summary>
    Task<Returning> RestoreVersionAsync(Guid versionPk);

    Task<ReturningList<VersionListItemDto>> GetVersionsAsync(Guid pagePk);

    Task<Returning<PageInfoDto>> GetPageInfoAsync(Guid pagePk);
    Task<Returning<Guid>> CreatePageAsync(Guid? parentPk, string title, string slug);
    Task<Returning> RenamePageAsync(Guid pagePk, string title);
    Task<Returning> MovePageAsync(Guid pagePk, Guid? newParentPk);
    Task<Returning> ReorderAsync(Guid pagePk, int sortOrder);

    /// <summary>Soft delete of the page and its whole subtree.</summary>
    Task<Returning> DeletePageAsync(Guid pagePk);

    Task<Returning<PagePermissionsDto>> GetPermissionsAsync(Guid pagePk);

    /// <summary>Replaces the page visibility: public flag + host permission names allowed to view.</summary>
    Task<Returning> SetPermissionsAsync(Guid pagePk, bool isPublic, IReadOnlyList<string> permissions);

    /// <summary>Search over title and published content, respecting permissions. Case-insensitive.</summary>
    Task<ReturningList<SearchResultDto>> SearchAsync(string term);
}
