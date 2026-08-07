using MgSoftDev.KnowledgeHub.Dtos;
using MgSoftDev.KnowledgeHub.Enums;
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
    /// <summary>
    /// Creates a page. Titles may repeat freely — including between siblings: the slug is derived
    /// from the title and gets a numeric suffix if that base is already taken, so this never fails
    /// over a name clash. Pass <paramref name="slug"/> only to force a specific one (the seeder
    /// does, to keep its permission mapping stable); it gets the same suffix treatment.
    /// </summary>
    Task<Returning<Guid>> CreatePageAsync(Guid? parentPk, string title, string? slug = null);
    Task<Returning> RenamePageAsync(Guid pagePk, string title);
    Task<Returning> MovePageAsync(Guid pagePk, Guid? newParentPk);
    /// <summary>
    /// Sets an explicit position. Kept for compatibility; prefer <see cref="MovePageOrderAsync"/>,
    /// which does not require the caller to know its siblings' numbers. Siblings are renumbered
    /// 1..N afterwards either way.
    /// </summary>
    Task<Returning> ReorderAsync(Guid pagePk, int sortOrder);

    /// <summary>
    /// Moves the page one position up or down among its siblings and renumbers the group 1..N.
    /// At either end it is a no-op and still returns success.
    /// </summary>
    Task<Returning> MovePageOrderAsync(Guid pagePk, PageMoveDirection direction);

    /// <summary>
    /// Renumbers EVERY sibling group in the tree to 1..N, preserving the order currently shown.
    /// Maintenance action for databases whose numbering drifted before renumbering was automatic;
    /// returns how many pages changed. Admin only.
    /// </summary>
    Task<Returning<int>> NormalizeAllPageOrdersAsync();

    /// <summary>Sets the page's icon (Material Symbols name) and icon color; both nullable to clear.</summary>
    Task<Returning> SetPageIconAsync(Guid pagePk, string? icon, string? iconColor);

    /// <summary>Soft delete of the page and its whole subtree.</summary>
    Task<Returning> DeletePageAsync(Guid pagePk);

    Task<Returning<PagePermissionsDto>> GetPermissionsAsync(Guid pagePk);

    /// <summary>Replaces the page visibility: public flag + host permission names allowed to view.</summary>
    Task<Returning> SetPermissionsAsync(Guid pagePk, bool isPublic, IReadOnlyList<string> permissions);

    /// <summary>Search over title and published content, respecting permissions. Case-insensitive.</summary>
    Task<ReturningList<SearchResultDto>> SearchAsync(string term);
}
