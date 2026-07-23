using MgSoftDev.KnowledgeHub.Contracts;
using MgSoftDev.KnowledgeHub.Dtos;
using MgSoftDev.KnowledgeHub.Entities;
using MgSoftDev.KnowledgeHub.Enums;
using MgSoftDev.KnowledgeHub.Store;
using MgSoftDev.ReturningCore;
using MgSoftDev.ReturningCore.Exceptions;
using MgSoftDev.ReturningCore.Helper;

namespace KnowledgeHub.ParityHarness;

/// <summary>
/// Dictionary-backed store used to prove the IKnowledgeHubStore abstraction is not biased
/// toward any engine. Register as SINGLETON — it owns the data for the whole run.
/// </summary>
public sealed class InMemoryKnowledgeHubStore : IKnowledgeHubStore
{
    private readonly Lock _gate = new();
    private readonly Dictionary<Guid, DocPage> _pages = new();
    private readonly Dictionary<Guid, DocPageVersion> _versions = new();
    private readonly Dictionary<Guid, DocImage> _images = new();
    private readonly Dictionary<Guid, byte[]> _contents = new(); // keyed by DocImage.Pk
    private readonly List<DocPagePermission> _permissions = new();
    private readonly List<DocPageDocImage> _pageImages = new();

    // ---------------------------------------------------------------- Pages: reads

    public Task<ReturningList<PageTreeNodeDto>> GetVisiblePagesAsync(VisibilityFilter filter) =>
        Task.FromResult(ReturningList<PageTreeNodeDto>.Try(() =>
        {
            lock (_gate)
                return _pages.Values
                    .Where(p => p.RowIsActive && MatchesFilter(p, filter))
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
                    .ToList();
        }));

    public Task<Returning<PageHeaderDto?>> GetVisiblePageHeaderAsync(Guid pagePk, VisibilityFilter filter) =>
        Task.FromResult(Returning<PageHeaderDto?>.Try(() =>
        {
            lock (_gate)
            {
                var page = _pages.Values.FirstOrDefault(p => p.Pk == pagePk && p.RowIsActive && MatchesFilter(p, filter));
                return page is null ? null : new PageHeaderDto(page.Pk, page.Title, page.Fk_DocPageVersionPublished, page.Icon, page.IconColor);
            }
        }));

    public Task<Returning<DocPage?>> GetPageAsync(Guid pagePk) =>
        Task.FromResult(Returning<DocPage?>.Try(() =>
        {
            lock (_gate)
                return _pages.Values.FirstOrDefault(p => p.Pk == pagePk && p.RowIsActive);
        }));

    public Task<ReturningList<PageLinkDto>> GetActivePageLinksAsync() =>
        Task.FromResult(ReturningList<PageLinkDto>.Try(() =>
        {
            lock (_gate)
                return _pages.Values.Where(p => p.RowIsActive)
                    .Select(p => new PageLinkDto(p.Pk, p.Fk_DocPageParent))
                    .ToList();
        }));

    // ---------------------------------------------------------------- Versions

    public Task<Returning<PageVersionDto?>> GetVersionAsync(Guid versionPk) =>
        Task.FromResult(Returning<PageVersionDto?>.Try(() =>
        {
            lock (_gate)
                return _versions.TryGetValue(versionPk, out var v) ? ToVersionDto(v) : null;
        }));

    public Task<Returning<PageVersionDto?>> GetLatestVersionAsync(Guid pagePk) =>
        Task.FromResult(Returning<PageVersionDto?>.Try(() =>
        {
            lock (_gate)
            {
                var latest = _versions.Values.Where(v => v.Fk_DocPage == pagePk)
                    .OrderByDescending(v => v.VersionNumber).FirstOrDefault();
                return latest is null ? null : ToVersionDto(latest);
            }
        }));

    public Task<Returning<int>> GetMaxVersionNumberAsync(Guid pagePk) =>
        Task.FromResult(Returning<int>.Try(() =>
        {
            lock (_gate)
                return _versions.Values.Where(v => v.Fk_DocPage == pagePk)
                    .Select(v => v.VersionNumber).DefaultIfEmpty(0).Max();
        }));

    public Task<ReturningList<VersionListItemDto>> GetVersionListAsync(Guid pagePk) =>
        Task.FromResult(ReturningList<VersionListItemDto>.Try(() =>
        {
            lock (_gate)
                return _versions.Values.Where(v => v.Fk_DocPage == pagePk)
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
                    .ToList();
        }));

    public Task<Returning> InsertVersionAsync(DocPageVersion version, IReadOnlyList<Guid> pageImagePks) =>
        Task.FromResult(Returning.Try(() =>
        {
            lock (_gate)
            {
                if (_versions.Values.Any(v => v.Fk_DocPage == version.Fk_DocPage && v.VersionNumber == version.VersionNumber))
                    throw new InvalidOperationException(
                        $"Duplicate version {version.VersionNumber} for page {version.Fk_DocPage}");

                _versions[version.Pk] = version;
                ReplacePageImageLinks(version.Fk_DocPage, pageImagePks, version.RowUserCreate, version.RowCreateDate);
                return Returning.Success();
            }
        }));

    public Task<Returning<PublishOutcome>> TryPublishAsync(Guid pagePk, int baseVersionNumber, AuditStamp audit) =>
        Task.FromResult(Returning<PublishOutcome>.Try(() =>
        {
            lock (_gate)
            {
                var page = _pages.Values.FirstOrDefault(p => p.Pk == pagePk && p.RowIsActive);
                if (page is null) return PublishOutcome.PageNotFound;

                var currentNumber = 0;
                if (page.Fk_DocPageVersionPublished is Guid publishedPk &&
                    _versions.TryGetValue(publishedPk, out var current))
                    currentNumber = current.VersionNumber;

                if (currentNumber > baseVersionNumber) return PublishOutcome.Conflict;

                var latest = _versions.Values.Where(v => v.Fk_DocPage == pagePk)
                    .OrderByDescending(v => v.VersionNumber).FirstOrDefault();
                if (latest is null) return PublishOutcome.NothingToPublish;

                latest.Status = DocPageStatus.Published;
                latest.PublishedAt = audit.Timestamp;
                Touch(latest, audit);

                page.Fk_DocPageVersionPublished = latest.Pk;
                page.Title = latest.Title;
                Touch(page, audit);

                return PublishOutcome.Published;
            }
        }));

    // ---------------------------------------------------------------- Page management

    public Task<Returning<bool>> SlugExistsAsync(string slug) =>
        Task.FromResult(Returning<bool>.Try(() =>
        {
            lock (_gate)
                return _pages.Values.Any(p => string.Equals(p.Slug, slug, StringComparison.OrdinalIgnoreCase));
        }));

    public Task<Returning<int>> GetMaxSortOrderAsync(Guid? parentPk) =>
        Task.FromResult(Returning<int>.Try(() =>
        {
            lock (_gate)
                return _pages.Values.Where(p => p.Fk_DocPageParent == parentPk)
                    .Select(p => p.SortOrder).DefaultIfEmpty(0).Max();
        }));

    public Task<Returning> InsertPageAsync(DocPage page) =>
        Task.FromResult(Returning.Try(() =>
        {
            lock (_gate)
            {
                _pages[page.Pk] = page;
                return Returning.Success();
            }
        }));

    public Task<Returning<bool>> RenamePageAsync(Guid pagePk, string title, AuditStamp audit) =>
        Task.FromResult(Returning<bool>.Try(() =>
        {
            lock (_gate)
            {
                var page = _pages.Values.FirstOrDefault(p => p.Pk == pagePk && p.RowIsActive);
                if (page is null) return false;
                page.Title = title;
                Touch(page, audit);
                return true;
            }
        }));

    public Task<Returning<bool>> MovePageAsync(Guid pagePk, Guid? newParentPk, AuditStamp audit) =>
        Task.FromResult(Returning<bool>.Try(() =>
        {
            lock (_gate)
            {
                var page = _pages.Values.FirstOrDefault(p => p.Pk == pagePk && p.RowIsActive);
                if (page is null) return false;
                page.Fk_DocPageParent = newParentPk;
                Touch(page, audit);
                return true;
            }
        }));

    public Task<Returning<bool>> SetSortOrderAsync(Guid pagePk, int sortOrder, AuditStamp audit) =>
        Task.FromResult(Returning<bool>.Try(() =>
        {
            lock (_gate)
            {
                var page = _pages.Values.FirstOrDefault(p => p.Pk == pagePk && p.RowIsActive);
                if (page is null) return false;
                page.SortOrder = sortOrder;
                Touch(page, audit);
                return true;
            }
        }));

    public Task<Returning<bool>> SetPageIconAsync(Guid pagePk, string? icon, string? iconColor, AuditStamp audit) =>
        Task.FromResult(Returning<bool>.Try(() =>
        {
            lock (_gate)
            {
                var page = _pages.Values.FirstOrDefault(p => p.Pk == pagePk && p.RowIsActive);
                if (page is null) return false;
                page.Icon = icon;
                page.IconColor = iconColor;
                Touch(page, audit);
                return true;
            }
        }));

    public Task<Returning<int>> SoftDeletePagesAsync(IReadOnlyCollection<Guid> pagePks, AuditStamp audit) =>
        Task.FromResult(Returning<int>.Try(() =>
        {
            lock (_gate)
            {
                var count = 0;
                foreach (var pk in pagePks)
                {
                    if (!_pages.TryGetValue(pk, out var page) || !page.RowIsActive) continue;
                    page.RowIsActive = false;
                    Touch(page, audit);
                    count++;
                }
                return count;
            }
        }));

    public Task<Returning<PagePermissionsDto?>> GetPagePermissionsAsync(Guid pagePk) =>
        Task.FromResult(Returning<PagePermissionsDto?>.Try(() =>
        {
            lock (_gate)
            {
                var page = _pages.Values.FirstOrDefault(p => p.Pk == pagePk && p.RowIsActive);
                // Typed null: a bare "return null" would yield a null Returning reference.
                if (page is null) return (PagePermissionsDto?)null;
                return new PagePermissionsDto
                {
                    IsPublic = page.IsPublic,
                    Permissions = _permissions
                        .Where(dp => dp.Fk_DocPage == pagePk && dp.RowIsActive)
                        .Select(dp => dp.Permission)
                        .ToList()
                };
            }
        }));

    public Task<Returning<bool>> SetPagePermissionsAsync(Guid pagePk, bool isPublic,
        IReadOnlyList<string> permissions, AuditStamp audit) =>
        Task.FromResult(Returning<bool>.Try(() =>
        {
            lock (_gate)
            {
                var page = _pages.Values.FirstOrDefault(p => p.Pk == pagePk && p.RowIsActive);
                if (page is null) return false;

                page.IsPublic = isPublic;
                Touch(page, audit);

                _permissions.RemoveAll(dp => dp.Fk_DocPage == pagePk);
                foreach (var permission in permissions)
                    _permissions.Add(new DocPagePermission
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
            }
        }));

    // ---------------------------------------------------------------- Search

    public Task<ReturningList<SearchCandidateDto>> SearchPublishedAsync(string term, VisibilityFilter filter) =>
        Task.FromResult(ReturningList<SearchCandidateDto>.Try(() =>
        {
            lock (_gate)
                return _pages.Values
                    .Where(p => p.RowIsActive && p.Fk_DocPageVersionPublished != null && MatchesFilter(p, filter))
                    .Select(p => new
                    {
                        Page = p,
                        Content = _versions.TryGetValue(p.Fk_DocPageVersionPublished!.Value, out var v) ? v.ContentHtml : null
                    })
                    .Where(x => x.Page.Title.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                                (x.Content is not null && x.Content.Contains(term, StringComparison.OrdinalIgnoreCase)))
                    .Select(x => new SearchCandidateDto(x.Page.Pk, x.Page.Title, x.Page.Slug, x.Content, x.Page.Icon, x.Page.IconColor))
                    .ToList();
        }));

    // ---------------------------------------------------------------- Images

    public Task<ReturningList<ImageRefDto>> GetImageRefsByHashesAsync(IReadOnlyCollection<string> contentHashes) =>
        Task.FromResult(ReturningList<ImageRefDto>.Try(() =>
        {
            var wanted = contentHashes.ToHashSet(StringComparer.OrdinalIgnoreCase);
            lock (_gate)
                return _images.Values.Where(i => wanted.Contains(i.ContentHash))
                    .Select(i => new ImageRefDto(i.Pk, i.ContentHash))
                    .ToList();
        }));

    public Task<Returning> InsertImageAsync(DocImage image, byte[] content) =>
        Task.FromResult(Returning.Try(() =>
        {
            lock (_gate)
            {
                _images[image.Pk] = image;
                _contents[image.Pk] = content;
                return Returning.Success();
            }
        }));

    public Task<ReturningList<ImageRefDto>> GetImageRefsAsync(IReadOnlyCollection<Guid> imagePks) =>
        Task.FromResult(ReturningList<ImageRefDto>.Try(() =>
        {
            lock (_gate)
                return imagePks.Where(_images.ContainsKey)
                    .Select(pk => new ImageRefDto(pk, _images[pk].ContentHash))
                    .ToList();
        }));

    public Task<ReturningList<ImageBlobDto>> GetImageContentsAsync(IReadOnlyCollection<Guid> imagePks) =>
        Task.FromResult(ReturningList<ImageBlobDto>.Try(() =>
        {
            lock (_gate)
                return imagePks.Where(_contents.ContainsKey)
                    .Select(pk => new ImageBlobDto(pk, _contents[pk]))
                    .ToList();
        }));

    public Task<Returning<ImageBlobDto?>> GetImageContentByHashAsync(string contentHash) =>
        Task.FromResult(Returning<ImageBlobDto?>.Try(() =>
        {
            lock (_gate)
            {
                var image = _images.Values.FirstOrDefault(i =>
                    string.Equals(i.ContentHash, contentHash, StringComparison.OrdinalIgnoreCase));
                if (image is null || !_contents.TryGetValue(image.Pk, out var content)) return (ImageBlobDto?)null;
                return new ImageBlobDto(image.Pk, content);
            }
        }));

    public Task<ReturningList<string>> GetAllImageHashesAsync() =>
        Task.FromResult(ReturningList<string>.Try(() =>
        {
            lock (_gate)
                return _images.Values.Select(i => i.ContentHash).ToList();
        }));

    public Task<ReturningList<Guid>> FilterExistingImagePksAsync(IReadOnlyCollection<Guid> imagePks) =>
        Task.FromResult(ReturningList<Guid>.Try(() =>
        {
            lock (_gate)
                return imagePks.Where(_images.ContainsKey).ToList();
        }));

    // ---------------------------------------------------------------- Helpers

    private bool MatchesFilter(DocPage page, VisibilityFilter filter) =>
        filter.SeesEverything || page.IsPublic ||
        _permissions.Any(dp => dp.Fk_DocPage == page.Pk && dp.RowIsActive &&
            filter.Permissions.Contains(dp.Permission, StringComparer.OrdinalIgnoreCase));

    private void ReplacePageImageLinks(Guid pagePk, IReadOnlyList<Guid> imagePks, string? userName, DateTime timestamp)
    {
        _pageImages.RemoveAll(l => l.Fk_DocPage == pagePk);
        foreach (var imagePk in imagePks.Distinct())
            _pageImages.Add(new DocPageDocImage
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

    private static void Touch(MgSoftDev.KnowledgeHub.Entities.EntityBase entity, AuditStamp audit)
    {
        entity.RowUpdateDate = audit.Timestamp;
        entity.RowUserUpdate = audit.UserName;
    }

    private static PageVersionDto ToVersionDto(DocPageVersion v) =>
        new(v.Pk, v.Fk_DocPage, v.VersionNumber, v.Title, v.ContentHtml, v.Status, v.PublishedAt);
}
