using MgSoftDev.KnowledgeHub.Contracts;
using MgSoftDev.KnowledgeHub.Dtos;
using MgSoftDev.KnowledgeHub.Entities;
using MgSoftDev.KnowledgeHub.Enums;
using MgSoftDev.KnowledgeHub.Store;
using MgSoftDev.ReturningCore;
using MgSoftDev.ReturningCore.Exceptions;
using MgSoftDev.ReturningCore.Helper;

namespace MgSoftDev.KnowledgeHub.Storage.LiteDb;

/// <summary>
/// LiteDB implementation of <see cref="IKnowledgeHubStore"/>. Single-process by design:
/// composed writes run synchronously inside the context's write lock wrapped in a LiteDB
/// transaction (LiteDB transactions are per-thread). Visibility and text comparisons that
/// need case-insensitivity are resolved in memory over the (small) candidate sets, which is
/// documented behavior for this provider.
/// </summary>
public sealed class LiteDbKnowledgeHubStore : IKnowledgeHubStore
{
    private readonly LiteDbKnowledgeHubContext _ctx;

    public LiteDbKnowledgeHubStore(LiteDbKnowledgeHubContext ctx)
    {
        _ctx = ctx;
    }

    // ---------------------------------------------------------------- Pages: reads

    public Task<ReturningList<PageTreeNodeDto>> GetVisiblePagesAsync(VisibilityFilter filter) =>
        Task.FromResult(ReturningList<PageTreeNodeDto>.Try(() =>
            ActivePagesVisibleTo(filter)
                .Select(p => new PageTreeNodeDto
                {
                    Pk = p.Pk,
                    Fk_DocPageParent = p.Fk_DocPageParent,
                    Title = p.Title,
                    Slug = p.Slug,
                    SortOrder = p.SortOrder,
                    IsPublic = p.IsPublic,
                    Icon = p.Icon,
                    IconColor = p.IconColor,
                    HasPublishedVersion = p.Fk_DocPageVersionPublished != null
                })
                .ToList()));

    public Task<Returning<PageHeaderDto?>> GetVisiblePageHeaderAsync(Guid pagePk, VisibilityFilter filter) =>
        Task.FromResult(Returning<PageHeaderDto?>.Try(() =>
        {
            var page = _ctx.Pages.FindById(pagePk);
            // The null must be TYPED: a bare "return null" in a Returning-targeted lambda yields
            // a null Returning reference instead of Ok-with-null-Value.
            if (page is null || !page.RowIsActive || !IsVisible(page, filter)) return (PageHeaderDto?)null;
            return new PageHeaderDto(page.Pk, page.Title, page.Fk_DocPageVersionPublished, page.Icon, page.IconColor);
        }));

    public Task<Returning<DocPage?>> GetPageAsync(Guid pagePk) =>
        Task.FromResult(Returning<DocPage?>.Try(() =>
        {
            var page = _ctx.Pages.FindById(pagePk);
            return page is { RowIsActive: true } ? page : null;
        }));

    public Task<ReturningList<PageLinkDto>> GetActivePageLinksAsync() =>
        Task.FromResult(ReturningList<PageLinkDto>.Try(() =>
            _ctx.Pages.Query().Where(p => p.RowIsActive).ToList()
                .Select(p => new PageLinkDto(p.Pk, p.Fk_DocPageParent))
                .ToList()));

    // ---------------------------------------------------------------- Versions

    public Task<Returning<PageVersionDto?>> GetVersionAsync(Guid versionPk) =>
        Task.FromResult(Returning<PageVersionDto?>.Try(() =>
        {
            var version = _ctx.Versions.FindById(versionPk);
            return version is null ? null : ToVersionDto(version);
        }));

    public Task<Returning<PageVersionDto?>> GetLatestVersionAsync(Guid pagePk) =>
        Task.FromResult(Returning<PageVersionDto?>.Try(() =>
        {
            var latest = _ctx.Versions.Query().Where(v => v.Fk_DocPage == pagePk).ToList()
                .OrderByDescending(v => v.VersionNumber).FirstOrDefault();
            return latest is null ? null : ToVersionDto(latest);
        }));

    public Task<Returning<int>> GetMaxVersionNumberAsync(Guid pagePk) =>
        Task.FromResult(Returning<int>.Try(() =>
            _ctx.Versions.Query().Where(v => v.Fk_DocPage == pagePk).ToList()
                .Select(v => v.VersionNumber).DefaultIfEmpty(0).Max()));

    public Task<ReturningList<VersionListItemDto>> GetVersionListAsync(Guid pagePk) =>
        Task.FromResult(ReturningList<VersionListItemDto>.Try(() =>
            _ctx.Versions.Query().Where(v => v.Fk_DocPage == pagePk).ToList()
                .OrderByDescending(v => v.VersionNumber)
                .Select(v => new VersionListItemDto
                {
                    Pk = v.Pk,
                    VersionNumber = v.VersionNumber,
                    AuthorName = v.RowUserCreate,
                    RowCreateDate = v.RowCreateDate,
                    Status = v.Status,
                    ChangeNote = v.ChangeNote,
                    PublishedAt = v.PublishedAt,
                    IsCurrentPublished = false
                })
                .ToList()));

    public Task<Returning> InsertVersionAsync(DocPageVersion version, IReadOnlyList<Guid> pageImagePks) =>
        Task.FromResult(Returning.Try(() =>
        {
            lock (_ctx.WriteLock)
            {
                return InTransaction(() =>
                {
                    if (_ctx.Versions.Exists(v =>
                            v.Fk_DocPage == version.Fk_DocPage && v.VersionNumber == version.VersionNumber))
                        throw new InvalidOperationException(
                            $"Duplicate version {version.VersionNumber} for page {version.Fk_DocPage}");

                    _ctx.Versions.Insert(version);
                    ReplacePageImageLinks(version.Fk_DocPage, pageImagePks, version.RowUserCreate, version.RowCreateDate);
                    return Returning.Success();
                });
            }
        }));

    public Task<Returning<PublishOutcome>> TryPublishAsync(Guid pagePk, int baseVersionNumber, AuditStamp audit) =>
        Task.FromResult(Returning<PublishOutcome>.Try(() =>
        {
            lock (_ctx.WriteLock)
            {
                return InTransaction(() =>
                {
                    var page = _ctx.Pages.FindById(pagePk);
                    if (page is null || !page.RowIsActive) return PublishOutcome.PageNotFound;

                    var currentNumber = 0;
                    if (page.Fk_DocPageVersionPublished is Guid publishedPk &&
                        _ctx.Versions.FindById(publishedPk) is { } current)
                        currentNumber = current.VersionNumber;

                    if (currentNumber > baseVersionNumber) return PublishOutcome.Conflict;

                    var latest = _ctx.Versions.Query().Where(v => v.Fk_DocPage == pagePk).ToList()
                        .OrderByDescending(v => v.VersionNumber).FirstOrDefault();
                    if (latest is null) return PublishOutcome.NothingToPublish;

                    latest.Status = DocPageStatus.Published;
                    latest.PublishedAt = audit.Timestamp;
                    Touch(latest, audit);
                    _ctx.Versions.Update(latest);

                    page.Fk_DocPageVersionPublished = latest.Pk;
                    page.Title = latest.Title;
                    Touch(page, audit);
                    _ctx.Pages.Update(page);

                    return PublishOutcome.Published;
                });
            }
        }));

    // ---------------------------------------------------------------- Page management

    public Task<Returning<bool>> SlugExistsAsync(string slug) =>
        Task.FromResult(Returning<bool>.Try(() =>
            _ctx.Pages.Query().Select(p => p.Slug).ToList()
                .Any(s => string.Equals(s, slug, StringComparison.OrdinalIgnoreCase))));

    public Task<Returning<int>> GetMaxSortOrderAsync(Guid? parentPk) =>
        Task.FromResult(Returning<int>.Try(() =>
            _ctx.Pages.Query().ToList()
                .Where(p => p.Fk_DocPageParent == parentPk)
                .Select(p => p.SortOrder).DefaultIfEmpty(0).Max()));

    public Task<Returning> InsertPageAsync(DocPage page) =>
        Task.FromResult(Returning.Try(() =>
        {
            lock (_ctx.WriteLock)
            {
                _ctx.Pages.Insert(page);
                return Returning.Success();
            }
        }));

    public Task<Returning<bool>> RenamePageAsync(Guid pagePk, string title, AuditStamp audit) =>
        Task.FromResult(Returning<bool>.Try(() =>
        {
            lock (_ctx.WriteLock)
            {
                var page = _ctx.Pages.FindById(pagePk);
                if (page is null || !page.RowIsActive) return false;
                page.Title = title;
                Touch(page, audit);
                _ctx.Pages.Update(page);
                return true;
            }
        }));

    public Task<Returning<bool>> MovePageAsync(Guid pagePk, Guid? newParentPk, AuditStamp audit) =>
        Task.FromResult(Returning<bool>.Try(() =>
        {
            lock (_ctx.WriteLock)
            {
                var page = _ctx.Pages.FindById(pagePk);
                if (page is null || !page.RowIsActive) return false;
                page.Fk_DocPageParent = newParentPk;
                Touch(page, audit);
                _ctx.Pages.Update(page);
                return true;
            }
        }));

    public Task<Returning<bool>> SetSortOrderAsync(Guid pagePk, int sortOrder, AuditStamp audit) =>
        Task.FromResult(Returning<bool>.Try(() =>
        {
            lock (_ctx.WriteLock)
            {
                var page = _ctx.Pages.FindById(pagePk);
                if (page is null || !page.RowIsActive) return false;
                page.SortOrder = sortOrder;
                Touch(page, audit);
                _ctx.Pages.Update(page);
                return true;
            }
        }));

    public Task<Returning<bool>> SetPageIconAsync(Guid pagePk, string? icon, string? iconColor, AuditStamp audit) =>
        Task.FromResult(Returning<bool>.Try(() =>
        {
            lock (_ctx.WriteLock)
            {
                var page = _ctx.Pages.FindById(pagePk);
                if (page is null || !page.RowIsActive) return false;
                page.Icon = icon;
                page.IconColor = iconColor;
                Touch(page, audit);
                _ctx.Pages.Update(page);
                return true;
            }
        }));

    public Task<Returning<int>> SoftDeletePagesAsync(IReadOnlyCollection<Guid> pagePks, AuditStamp audit) =>
        Task.FromResult(Returning<int>.Try(() =>
        {
            lock (_ctx.WriteLock)
            {
                return InTransaction(() =>
                {
                    var count = 0;
                    foreach (var pk in pagePks)
                    {
                        var page = _ctx.Pages.FindById(pk);
                        if (page is null || !page.RowIsActive) continue;
                        page.RowIsActive = false;
                        Touch(page, audit);
                        _ctx.Pages.Update(page);
                        count++;
                    }
                    return count;
                });
            }
        }));

    public Task<Returning<PagePermissionsDto?>> GetPagePermissionsAsync(Guid pagePk) =>
        Task.FromResult(Returning<PagePermissionsDto?>.Try(() =>
        {
            var page = _ctx.Pages.FindById(pagePk);
            if (page is null || !page.RowIsActive) return (PagePermissionsDto?)null;
            return new PagePermissionsDto
            {
                IsPublic = page.IsPublic,
                Permissions = _ctx.PagePermissions.Query().Where(dp => dp.Fk_DocPage == pagePk).ToList()
                    .Where(dp => dp.RowIsActive)
                    .Select(dp => dp.Permission)
                    .ToList()
            };
        }));

    public Task<Returning<bool>> SetPagePermissionsAsync(Guid pagePk, bool isPublic,
        IReadOnlyList<string> permissions, AuditStamp audit) =>
        Task.FromResult(Returning<bool>.Try(() =>
        {
            lock (_ctx.WriteLock)
            {
                return InTransaction(() =>
                {
                    var page = _ctx.Pages.FindById(pagePk);
                    if (page is null || !page.RowIsActive) return false;

                    page.IsPublic = isPublic;
                    Touch(page, audit);
                    _ctx.Pages.Update(page);

                    _ctx.PagePermissions.DeleteMany(dp => dp.Fk_DocPage == pagePk);
                    foreach (var permission in permissions)
                        _ctx.PagePermissions.Insert(new DocPagePermission
                        {
                            Pk = Guid.CreateVersion7(),
                            Fk_DocPage = pagePk,
                            Permission = permission,
                            RowIsActive = true,
                            RowCreateDate = audit.Timestamp,
                            RowUpdateDate = audit.Timestamp,
                            RowUserCreate = audit.UserName,
                            RowUserUpdate = audit.UserName
                        });
                    return true;
                });
            }
        }));

    // ---------------------------------------------------------------- Search

    public Task<ReturningList<SearchCandidateDto>> SearchPublishedAsync(string term, VisibilityFilter filter) =>
        Task.FromResult(ReturningList<SearchCandidateDto>.Try(() =>
            ActivePagesVisibleTo(filter)
                .Where(p => p.Fk_DocPageVersionPublished != null)
                .Select(p => new
                {
                    Page = p,
                    Content = _ctx.Versions.FindById(p.Fk_DocPageVersionPublished!.Value)?.ContentHtml
                })
                .Where(x => x.Page.Title.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                            (x.Content is not null && x.Content.Contains(term, StringComparison.OrdinalIgnoreCase)))
                .Select(x => new SearchCandidateDto(x.Page.Pk, x.Page.Title, x.Page.Slug, x.Content, x.Page.Icon, x.Page.IconColor))
                .ToList()));

    // ---------------------------------------------------------------- Images

    public Task<ReturningList<ImageRefDto>> GetImageRefsByHashesAsync(IReadOnlyCollection<string> contentHashes) =>
        Task.FromResult(ReturningList<ImageRefDto>.Try(() =>
        {
            var wanted = contentHashes.ToHashSet(StringComparer.OrdinalIgnoreCase);
            // Metadata-only collection: the binary lives elsewhere, so a scan stays cheap.
            return _ctx.Images.Query().ToList()
                .Where(i => wanted.Contains(i.ContentHash))
                .Select(i => new ImageRefDto(i.Pk, i.ContentHash))
                .ToList();
        }));

    public Task<Returning> InsertImageAsync(DocImage image, byte[] content) =>
        Task.FromResult(Returning.Try(() =>
        {
            lock (_ctx.WriteLock)
            {
                return InTransaction(() =>
                {
                    _ctx.Images.Insert(image);
                    _ctx.ImageContents.Insert(new DocImageContent
                    {
                        Pk = Guid.CreateVersion7(),
                        Fk_DocImage = image.Pk,
                        Content = content,
                        RowIsActive = true,
                        RowCreateDate = image.RowCreateDate,
                        RowUpdateDate = image.RowUpdateDate,
                        RowUserCreate = image.RowUserCreate,
                        RowUserUpdate = image.RowUserUpdate
                    });
                    return Returning.Success();
                });
            }
        }));

    public Task<ReturningList<ImageRefDto>> GetImageRefsAsync(IReadOnlyCollection<Guid> imagePks) =>
        Task.FromResult(ReturningList<ImageRefDto>.Try(() =>
            imagePks.Select(pk => _ctx.Images.FindById(pk))
                .Where(i => i is not null)
                .Select(i => new ImageRefDto(i!.Pk, i.ContentHash))
                .ToList()));

    public Task<ReturningList<ImageBlobDto>> GetImageContentsAsync(IReadOnlyCollection<Guid> imagePks) =>
        Task.FromResult(ReturningList<ImageBlobDto>.Try(() =>
        {
            var result = new List<ImageBlobDto>();
            foreach (var pk in imagePks)
            {
                var content = _ctx.ImageContents.Query().Where(c => c.Fk_DocImage == pk).FirstOrDefault();
                if (content is not null)
                    result.Add(new ImageBlobDto(pk, content.Content));
            }
            return result;
        }));

    public Task<Returning<ImageBlobDto?>> GetImageContentByHashAsync(string contentHash) =>
        Task.FromResult(Returning<ImageBlobDto?>.Try(() =>
        {
            var image = _ctx.Images.Query().ToList()
                .FirstOrDefault(i => string.Equals(i.ContentHash, contentHash, StringComparison.OrdinalIgnoreCase));
            if (image is null) return (ImageBlobDto?)null;

            var content = _ctx.ImageContents.Query().Where(c => c.Fk_DocImage == image.Pk).FirstOrDefault();
            return content is null ? null : new ImageBlobDto(image.Pk, content.Content);
        }));

    public Task<ReturningList<string>> GetAllImageHashesAsync() =>
        Task.FromResult(ReturningList<string>.Try(() =>
            _ctx.Images.Query().Select(i => i.ContentHash).ToList()));

    public Task<ReturningList<Guid>> FilterExistingImagePksAsync(IReadOnlyCollection<Guid> imagePks) =>
        Task.FromResult(ReturningList<Guid>.Try(() =>
            imagePks.Where(pk => _ctx.Images.FindById(pk) is not null).ToList()));

    // ---------------------------------------------------------------- Helpers

    /// <summary>
    /// Active pages passing the visibility filter. LiteDB has no joins and its string index is
    /// case-sensitive, so the permission match runs in memory over the permission rows.
    /// </summary>
    private List<DocPage> ActivePagesVisibleTo(VisibilityFilter filter)
    {
        var active = _ctx.Pages.Query().Where(p => p.RowIsActive).ToList();
        if (filter.SeesEverything) return active;

        var granted = _ctx.PagePermissions.Query().ToList()
            .Where(dp => dp.RowIsActive &&
                         filter.Permissions.Contains(dp.Permission, StringComparer.OrdinalIgnoreCase))
            .Select(dp => dp.Fk_DocPage)
            .ToHashSet();

        return active.Where(p => p.IsPublic || granted.Contains(p.Pk)).ToList();
    }

    private bool IsVisible(DocPage page, VisibilityFilter filter) =>
        filter.SeesEverything || page.IsPublic ||
        _ctx.PagePermissions.Query().Where(dp => dp.Fk_DocPage == page.Pk).ToList()
            .Any(dp => dp.RowIsActive &&
                       filter.Permissions.Contains(dp.Permission, StringComparer.OrdinalIgnoreCase));

    private void ReplacePageImageLinks(Guid pagePk, IReadOnlyList<Guid> imagePks, string? userName, DateTime timestamp)
    {
        _ctx.PageImages.DeleteMany(l => l.Fk_DocPage == pagePk);
        foreach (var imagePk in imagePks.Distinct())
            _ctx.PageImages.Insert(new DocPageDocImage
            {
                Pk = Guid.CreateVersion7(),
                Fk_DocPage = pagePk,
                Fk_DocImage = imagePk,
                RowIsActive = true,
                RowCreateDate = timestamp,
                RowUpdateDate = timestamp,
                RowUserCreate = userName,
                RowUserUpdate = userName
            });
    }

    /// <summary>
    /// Runs a composed write inside a LiteDB transaction. Must be called while holding the
    /// write lock and with fully synchronous work (LiteDB transactions are per-thread).
    /// </summary>
    private T InTransaction<T>(Func<T> work)
    {
        _ctx.Database.BeginTrans();
        try
        {
            var result = work();
            _ctx.Database.Commit();
            return result;
        }
        catch
        {
            _ctx.Database.Rollback();
            throw;
        }
    }

    private static void Touch(EntityBase entity, AuditStamp audit)
    {
        entity.RowUpdateDate = audit.Timestamp;
        entity.RowUserUpdate = audit.UserName;
    }

    private static PageVersionDto ToVersionDto(DocPageVersion v) =>
        new(v.Pk, v.Fk_DocPage, v.VersionNumber, v.Title, v.ContentHtml, v.Status, v.PublishedAt);
}
