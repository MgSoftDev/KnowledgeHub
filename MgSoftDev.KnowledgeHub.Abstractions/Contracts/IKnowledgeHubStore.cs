using MgSoftDev.KnowledgeHub.Dtos;
using MgSoftDev.KnowledgeHub.Entities;
using MgSoftDev.KnowledgeHub.Store;
using MgSoftDev.ReturningCore;

namespace MgSoftDev.KnowledgeHub.Contracts;

/// <summary>
/// Storage provider contract. Official providers exist for SQL Server and LiteDB; a host may
/// also implement this interface to plug KnowledgeHub into its own persistence layer.
///
/// Conventions every implementation MUST honor:
/// <list type="bullet">
/// <item>Find-style methods return Ok with a null Value when the row does not exist; the core
/// services translate that into business messages. Providers never throw for "not found".</item>
/// <item>Visibility filters are applied INSIDE the query, never after materialization.</item>
/// <item>Permission and hash comparisons are case-insensitive.</item>
/// <item>All entity values (Pk, Row* audit columns) arrive fully populated by the core services;
/// providers never generate ids, timestamps or audit values except where an
/// <see cref="AuditStamp"/> parameter says so.</item>
/// <item>Providers do not call SaveLog — the service boundary logs.</item>
/// </list>
/// </summary>
public interface IKnowledgeHubStore
{
    // ---------------------------------------------------------------- Pages: reads

    /// <summary>
    /// Flat list of active pages visible to the filter (visible = IsPublic OR any active
    /// DocPagePermission.Permission ∈ filter.Permissions; SeesEverything bypasses). No
    /// hierarchy: the core builds the tree and applies ancestor-chain inheritance.
    /// Children collections come empty; HasPublishedVersion must be computed in the query.
    /// </summary>
    Task<ReturningList<PageTreeNodeDto>> GetVisiblePagesAsync(VisibilityFilter filter);

    /// <summary>Header of one active page IF visible to the filter; null Value otherwise.</summary>
    Task<Returning<PageHeaderDto?>> GetVisiblePageHeaderAsync(Guid pagePk, VisibilityFilter filter);

    /// <summary>Active page by pk, unfiltered (edit/management paths, already authorized by the core).</summary>
    Task<Returning<DocPage?>> GetPageAsync(Guid pagePk);

    /// <summary>(Pk, ParentPk) of all active pages; used for cycle detection and subtree collection.</summary>
    Task<ReturningList<PageLinkDto>> GetActivePageLinksAsync();

    // ---------------------------------------------------------------- Versions

    Task<Returning<PageVersionDto?>> GetVersionAsync(Guid versionPk);

    /// <summary>Highest-numbered version of the page (draft or published); null Value if none.</summary>
    Task<Returning<PageVersionDto?>> GetLatestVersionAsync(Guid pagePk);

    /// <summary>0 when the page has no versions.</summary>
    Task<Returning<int>> GetMaxVersionNumberAsync(Guid pagePk);

    /// <summary>
    /// All versions of the page, descending by VersionNumber. AuthorName is filled from
    /// RowUserCreate; IsCurrentPublished arrives false (the core marks it).
    /// </summary>
    Task<ReturningList<VersionListItemDto>> GetVersionListAsync(Guid pagePk);

    /// <summary>
    /// Atomic unit: insert the version row AND replace the page↔image links of the page with
    /// exactly <paramref name="pageImagePks"/> (audit columns of new links taken from the
    /// version row). Must fail if (Fk_DocPage, VersionNumber) already exists (unique index).
    /// </summary>
    Task<Returning> InsertVersionAsync(DocPageVersion version, IReadOnlyList<Guid> pageImagePks);

    /// <summary>
    /// Atomic per engine. Algorithm:
    /// 1) load active page (else PageNotFound);
    /// 2) resolve the currently published VersionNumber (0 if none);
    /// 3) if current &gt; <paramref name="baseVersionNumber"/> return Conflict;
    /// 4) take the latest version by max VersionNumber (else NothingToPublish);
    /// 5) latest.Status = Published, PublishedAt = audit.Timestamp, audit columns updated;
    /// 6) page.Fk_DocPageVersionPublished = latest.Pk, page.Title = latest.Title, audit columns
    ///    updated; commit.
    /// </summary>
    Task<Returning<PublishOutcome>> TryPublishAsync(Guid pagePk, int baseVersionNumber, AuditStamp audit);

    // ---------------------------------------------------------------- Page management

    /// <summary>True when any page (active or not) already uses the slug.</summary>
    Task<Returning<bool>> SlugExistsAsync(string slug);

    /// <summary>Max SortOrder among the children of <paramref name="parentPk"/> (0 if none).</summary>
    Task<Returning<int>> GetMaxSortOrderAsync(Guid? parentPk);

    /// <summary>Inserts a page fully populated by the core (Pk, audit columns included).</summary>
    Task<Returning> InsertPageAsync(DocPage page);

    /// <summary>False when the active page does not exist.</summary>
    Task<Returning<bool>> RenamePageAsync(Guid pagePk, string title, AuditStamp audit);

    /// <summary>False when the active page does not exist. Cycle validation is done by the core.</summary>
    Task<Returning<bool>> MovePageAsync(Guid pagePk, Guid? newParentPk, AuditStamp audit);

    /// <summary>False when the active page does not exist.</summary>
    Task<Returning<bool>> SetSortOrderAsync(Guid pagePk, int sortOrder, AuditStamp audit);

    /// <summary>
    /// Sets the page's icon and icon color (both nullable to clear). False when the active page
    /// does not exist.
    /// </summary>
    Task<Returning<bool>> SetPageIconAsync(Guid pagePk, string? icon, string? iconColor, AuditStamp audit);

    /// <summary>
    /// Soft-deletes (RowIsActive = false + audit) every page in <paramref name="pagePks"/>.
    /// The subtree is computed by the core. Returns the number of pages actually deactivated.
    /// </summary>
    Task<Returning<int>> SoftDeletePagesAsync(IReadOnlyCollection<Guid> pagePks, AuditStamp audit);

    /// <summary>Visibility of one active page (IsPublic + active permission names); null Value if not found.</summary>
    Task<Returning<PagePermissionsDto?>> GetPagePermissionsAsync(Guid pagePk);

    /// <summary>
    /// Replace-all semantics: sets IsPublic (+ audit on the page row) and replaces every
    /// DocPagePermission of the page with <paramref name="permissions"/>.
    /// False when the active page does not exist.
    /// </summary>
    Task<Returning<bool>> SetPagePermissionsAsync(Guid pagePk, bool isPublic,
        IReadOnlyList<string> permissions, AuditStamp audit);

    // ---------------------------------------------------------------- Search

    /// <summary>
    /// Case-insensitive (invariant) match of <paramref name="term"/> against Title OR the
    /// published ContentHtml, over active + published pages passing the visibility filter.
    /// Returns the full ContentHtml (the core builds the snippet). No ancestor inheritance
    /// is applied here (parity with the original behavior).
    /// </summary>
    Task<ReturningList<SearchCandidateDto>> SearchPublishedAsync(string term, VisibilityFilter filter);

    // ---------------------------------------------------------------- Images

    /// <summary>Pk + ContentHash of the images whose hash is in <paramref name="contentHashes"/> (lower hex).</summary>
    Task<ReturningList<ImageRefDto>> GetImageRefsByHashesAsync(IReadOnlyCollection<string> contentHashes);

    /// <summary>Atomic pair: metadata + binary in one unit (possible thanks to client-generated Pk).</summary>
    Task<Returning> InsertImageAsync(DocImage image, byte[] content);

    /// <summary>Pk + ContentHash only. MUST NOT materialize the binary column.</summary>
    Task<ReturningList<ImageRefDto>> GetImageRefsAsync(IReadOnlyCollection<Guid> imagePks);

    /// <summary>All requested binaries in ONE batch query.</summary>
    Task<ReturningList<ImageBlobDto>> GetImageContentsAsync(IReadOnlyCollection<Guid> imagePks);

    /// <summary>Binary of the image with the given hash (lower hex); null Value if not found.</summary>
    Task<Returning<ImageBlobDto?>> GetImageContentByHashAsync(string contentHash);

    /// <summary>Every ContentHash present in DocImages (orphan cleanup).</summary>
    Task<ReturningList<string>> GetAllImageHashesAsync();

    /// <summary>Subset of <paramref name="imagePks"/> that actually exists in DocImages (FK safety on link sync).</summary>
    Task<ReturningList<Guid>> FilterExistingImagePksAsync(IReadOnlyCollection<Guid> imagePks);
}
