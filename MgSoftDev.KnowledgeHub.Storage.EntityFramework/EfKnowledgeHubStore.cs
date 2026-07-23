using MgSoftDev.KnowledgeHub.Contracts;
using MgSoftDev.KnowledgeHub.Dtos;
using MgSoftDev.KnowledgeHub.Entities;
using MgSoftDev.KnowledgeHub.Enums;
using MgSoftDev.KnowledgeHub.Store;
using MgSoftDev.ReturningCore;
using MgSoftDev.ReturningCore.Exceptions;
using MgSoftDev.ReturningCore.Helper;
using Microsoft.EntityFrameworkCore;

namespace MgSoftDev.KnowledgeHub.Storage.EntityFramework;

/// <summary>
/// EF Core implementation of <see cref="IKnowledgeHubStore"/>: short-lived contexts from the
/// factory, AsNoTracking on every read, visibility applied inside the query, and the publish
/// flow wrapped in an explicit transaction. Case-insensitivity of permission/slug/search
/// comparisons is provided by the database collation (SQL Server default collations are CI).
/// </summary>
public sealed class EfKnowledgeHubStore : IKnowledgeHubStore
{
    private readonly IDbContextFactory<KnowledgeHubDbContext> _factory;

    public EfKnowledgeHubStore(IDbContextFactory<KnowledgeHubDbContext> factory)
    {
        _factory = factory;
    }

    // ---------------------------------------------------------------- Pages: reads

    public Task<ReturningList<PageTreeNodeDto>> GetVisiblePagesAsync(VisibilityFilter filter) =>
        ReturningList<PageTreeNodeDto>.TryTask(async () =>
        {
            await using var db = await _factory.CreateDbContextAsync();

            var query = ApplyVisibility(db.Pages.AsNoTracking().Where(p => p.RowIsActive), filter);
            return await query
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
                .ToListAsync();
        });

    public Task<Returning<PageHeaderDto?>> GetVisiblePageHeaderAsync(Guid pagePk, VisibilityFilter filter) =>
        Returning<PageHeaderDto?>.TryTask(async () =>
        {
            await using var db = await _factory.CreateDbContextAsync();

            var query = ApplyVisibility(db.Pages.AsNoTracking().Where(p => p.Pk == pagePk && p.RowIsActive), filter);
            var page = await query
                .Select(p => new { p.Pk, p.Title, p.Fk_DocPageVersionPublished, p.Icon, p.IconColor })
                .FirstOrDefaultAsync();

            return page is null
                ? null
                : new PageHeaderDto(page.Pk, page.Title, page.Fk_DocPageVersionPublished, page.Icon, page.IconColor);
        });

    public Task<Returning<DocPage?>> GetPageAsync(Guid pagePk) =>
        Returning<DocPage?>.TryTask(async () =>
        {
            await using var db = await _factory.CreateDbContextAsync();
            return await db.Pages.AsNoTracking().FirstOrDefaultAsync(p => p.Pk == pagePk && p.RowIsActive);
        });

    public Task<ReturningList<PageLinkDto>> GetActivePageLinksAsync() =>
        ReturningList<PageLinkDto>.TryTask(async () =>
        {
            await using var db = await _factory.CreateDbContextAsync();
            return await db.Pages.AsNoTracking()
                .Where(p => p.RowIsActive)
                .Select(p => new PageLinkDto(p.Pk, p.Fk_DocPageParent))
                .ToListAsync();
        });

    // ---------------------------------------------------------------- Versions

    public Task<Returning<PageVersionDto?>> GetVersionAsync(Guid versionPk) =>
        Returning<PageVersionDto?>.TryTask(async () =>
        {
            await using var db = await _factory.CreateDbContextAsync();
            return await db.Versions.AsNoTracking()
                .Where(v => v.Pk == versionPk)
                .Select(v => new PageVersionDto(v.Pk, v.Fk_DocPage, v.VersionNumber, v.Title, v.ContentHtml, v.Status, v.PublishedAt))
                .FirstOrDefaultAsync();
        });

    public Task<Returning<PageVersionDto?>> GetLatestVersionAsync(Guid pagePk) =>
        Returning<PageVersionDto?>.TryTask(async () =>
        {
            await using var db = await _factory.CreateDbContextAsync();
            return await db.Versions.AsNoTracking()
                .Where(v => v.Fk_DocPage == pagePk)
                .OrderByDescending(v => v.VersionNumber)
                .Select(v => new PageVersionDto(v.Pk, v.Fk_DocPage, v.VersionNumber, v.Title, v.ContentHtml, v.Status, v.PublishedAt))
                .FirstOrDefaultAsync();
        });

    public Task<Returning<int>> GetMaxVersionNumberAsync(Guid pagePk) =>
        Returning<int>.TryTask(async () =>
        {
            await using var db = await _factory.CreateDbContextAsync();
            return await db.Versions.AsNoTracking()
                .Where(v => v.Fk_DocPage == pagePk)
                .MaxAsync(v => (int?)v.VersionNumber) ?? 0;
        });

    public Task<ReturningList<VersionListItemDto>> GetVersionListAsync(Guid pagePk) =>
        ReturningList<VersionListItemDto>.TryTask(async () =>
        {
            await using var db = await _factory.CreateDbContextAsync();
            return await db.Versions.AsNoTracking()
                .Where(v => v.Fk_DocPage == pagePk)
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
                .ToListAsync();
        });

    public Task<Returning> InsertVersionAsync(DocPageVersion version, IReadOnlyList<Guid> pageImagePks) =>
        Returning.TryTask(async () =>
        {
            await using var db = await _factory.CreateDbContextAsync();

            // Version row + page↔image link replacement commit in ONE SaveChanges (atomic).
            db.Versions.Add(version);

            var existing = await db.PageImages.Where(l => l.Fk_DocPage == version.Fk_DocPage).ToListAsync();
            db.PageImages.RemoveRange(existing);
            foreach (var imagePk in pageImagePks.Distinct())
                db.PageImages.Add(new DocPageDocImage
                {
                    Pk = Guid.CreateVersion7(),
                    Fk_DocPage = version.Fk_DocPage,
                    Fk_DocImage = imagePk,
                    RowIsActive = true,
                    RowCreateDate = version.RowCreateDate,
                    RowUpdateDate = version.RowUpdateDate,
                    RowUserCreate = version.RowUserCreate,
                    RowUserUpdate = version.RowUserUpdate
                });

            await db.SaveChangesAsync();
            return Returning.Success();
        });

    public Task<Returning<PublishOutcome>> TryPublishAsync(Guid pagePk, int baseVersionNumber, AuditStamp audit) =>
        Returning<PublishOutcome>.TryTask(async () =>
        {
            await using var db = await _factory.CreateDbContextAsync();
            await using var tx = await db.Database.BeginTransactionAsync();

            var page = await db.Pages.FirstOrDefaultAsync(p => p.Pk == pagePk && p.RowIsActive);
            if (page is null) return PublishOutcome.PageNotFound;

            // Conflict detection: has someone published a version newer than our baseline?
            var currentNumber = 0;
            if (page.Fk_DocPageVersionPublished is Guid publishedPk)
                currentNumber = await db.Versions
                    .Where(v => v.Pk == publishedPk)
                    .Select(v => v.VersionNumber)
                    .FirstOrDefaultAsync();

            if (currentNumber > baseVersionNumber) return PublishOutcome.Conflict;

            var latest = await db.Versions
                .Where(v => v.Fk_DocPage == pagePk)
                .OrderByDescending(v => v.VersionNumber)
                .FirstOrDefaultAsync();
            if (latest is null) return PublishOutcome.NothingToPublish;

            latest.Status = DocPageStatus.Published;
            latest.PublishedAt = audit.Timestamp;
            latest.RowUpdateDate = audit.Timestamp;
            latest.RowUserUpdate = audit.UserName;

            page.Fk_DocPageVersionPublished = latest.Pk;
            page.Title = latest.Title;
            page.RowUpdateDate = audit.Timestamp;
            page.RowUserUpdate = audit.UserName;

            await db.SaveChangesAsync();
            await tx.CommitAsync();
            return PublishOutcome.Published;
        });

    // ---------------------------------------------------------------- Page management

    public Task<Returning<bool>> SlugExistsAsync(string slug) =>
        Returning<bool>.TryTask(async () =>
        {
            await using var db = await _factory.CreateDbContextAsync();
            return await db.Pages.AsNoTracking().AnyAsync(p => p.Slug == slug);
        });

    public Task<Returning<int>> GetMaxSortOrderAsync(Guid? parentPk) =>
        Returning<int>.TryTask(async () =>
        {
            await using var db = await _factory.CreateDbContextAsync();
            return await db.Pages.AsNoTracking()
                .Where(p => p.Fk_DocPageParent == parentPk)
                .MaxAsync(p => (int?)p.SortOrder) ?? 0;
        });

    public Task<Returning> InsertPageAsync(DocPage page) =>
        Returning.TryTask(async () =>
        {
            await using var db = await _factory.CreateDbContextAsync();
            db.Pages.Add(page);
            await db.SaveChangesAsync();
            return Returning.Success();
        });

    public Task<Returning<bool>> RenamePageAsync(Guid pagePk, string title, AuditStamp audit) =>
        Returning<bool>.TryTask(async () =>
        {
            await using var db = await _factory.CreateDbContextAsync();
            var page = await db.Pages.FirstOrDefaultAsync(p => p.Pk == pagePk && p.RowIsActive);
            if (page is null) return false;

            page.Title = title;
            Touch(page, audit);
            await db.SaveChangesAsync();
            return true;
        });

    public Task<Returning<bool>> MovePageAsync(Guid pagePk, Guid? newParentPk, AuditStamp audit) =>
        Returning<bool>.TryTask(async () =>
        {
            await using var db = await _factory.CreateDbContextAsync();
            var page = await db.Pages.FirstOrDefaultAsync(p => p.Pk == pagePk && p.RowIsActive);
            if (page is null) return false;

            page.Fk_DocPageParent = newParentPk;
            Touch(page, audit);
            await db.SaveChangesAsync();
            return true;
        });

    public Task<Returning<bool>> SetSortOrderAsync(Guid pagePk, int sortOrder, AuditStamp audit) =>
        Returning<bool>.TryTask(async () =>
        {
            await using var db = await _factory.CreateDbContextAsync();
            var page = await db.Pages.FirstOrDefaultAsync(p => p.Pk == pagePk && p.RowIsActive);
            if (page is null) return false;

            page.SortOrder = sortOrder;
            Touch(page, audit);
            await db.SaveChangesAsync();
            return true;
        });

    public Task<Returning<bool>> SetPageIconAsync(Guid pagePk, string? icon, string? iconColor, AuditStamp audit) =>
        Returning<bool>.TryTask(async () =>
        {
            await using var db = await _factory.CreateDbContextAsync();
            var page = await db.Pages.FirstOrDefaultAsync(p => p.Pk == pagePk && p.RowIsActive);
            if (page is null) return false;

            page.Icon = icon;
            page.IconColor = iconColor;
            Touch(page, audit);
            await db.SaveChangesAsync();
            return true;
        });

    public Task<Returning<int>> SoftDeletePagesAsync(IReadOnlyCollection<Guid> pagePks, AuditStamp audit) =>
        Returning<int>.TryTask(async () =>
        {
            var pks = pagePks.ToList();

            await using var db = await _factory.CreateDbContextAsync();
            var pages = await db.Pages.Where(p => pks.Contains(p.Pk) && p.RowIsActive).ToListAsync();

            foreach (var page in pages)
            {
                page.RowIsActive = false;
                Touch(page, audit);
            }
            await db.SaveChangesAsync();
            return pages.Count;
        });

    public Task<Returning<PagePermissionsDto?>> GetPagePermissionsAsync(Guid pagePk) =>
        Returning<PagePermissionsDto?>.TryTask(async () =>
        {
            await using var db = await _factory.CreateDbContextAsync();

            var page = await db.Pages.AsNoTracking()
                .Where(p => p.Pk == pagePk && p.RowIsActive)
                .Select(p => new { p.IsPublic })
                .FirstOrDefaultAsync();
            // The null must be TYPED: a bare "return null" in a Returning-targeted lambda yields
            // a null Returning reference instead of Ok-with-null-Value.
            if (page is null) return (PagePermissionsDto?)null;

            var permissions = await db.PagePermissions.AsNoTracking()
                .Where(dp => dp.Fk_DocPage == pagePk && dp.RowIsActive)
                .Select(dp => dp.Permission)
                .ToListAsync();

            return new PagePermissionsDto { IsPublic = page.IsPublic, Permissions = permissions };
        });

    public Task<Returning<bool>> SetPagePermissionsAsync(Guid pagePk, bool isPublic,
        IReadOnlyList<string> permissions, AuditStamp audit) =>
        Returning<bool>.TryTask(async () =>
        {
            await using var db = await _factory.CreateDbContextAsync();

            var page = await db.Pages.FirstOrDefaultAsync(p => p.Pk == pagePk && p.RowIsActive);
            if (page is null) return false;

            page.IsPublic = isPublic;
            Touch(page, audit);

            var existing = await db.PagePermissions.Where(dp => dp.Fk_DocPage == pagePk).ToListAsync();
            db.PagePermissions.RemoveRange(existing);

            foreach (var permission in permissions)
                db.PagePermissions.Add(new DocPagePermission
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

            await db.SaveChangesAsync();
            return true;
        });

    // ---------------------------------------------------------------- Search

    public Task<ReturningList<SearchCandidateDto>> SearchPublishedAsync(string term, VisibilityFilter filter) =>
        ReturningList<SearchCandidateDto>.TryTask(async () =>
        {
            await using var db = await _factory.CreateDbContextAsync();

            var query = ApplyVisibility(
                db.Pages.AsNoTracking().Where(p => p.RowIsActive && p.Fk_DocPageVersionPublished != null),
                filter);

            var candidates = await query
                .Select(p => new
                {
                    p.Pk,
                    p.Title,
                    p.Slug,
                    p.Icon,
                    p.IconColor,
                    Content = db.Versions
                        .Where(v => v.Pk == p.Fk_DocPageVersionPublished)
                        .Select(v => v.ContentHtml)
                        .FirstOrDefault()
                })
                .Where(x => x.Title.Contains(term) || (x.Content != null && x.Content.Contains(term)))
                .ToListAsync();

            return candidates
                .Select(x => new SearchCandidateDto(x.Pk, x.Title, x.Slug, x.Content, x.Icon, x.IconColor))
                .ToList();
        });

    // ---------------------------------------------------------------- Images

    public Task<ReturningList<ImageRefDto>> GetImageRefsByHashesAsync(IReadOnlyCollection<string> contentHashes) =>
        ReturningList<ImageRefDto>.TryTask(async () =>
        {
            var hashes = contentHashes.ToList();

            await using var db = await _factory.CreateDbContextAsync();
            return await db.Images.AsNoTracking()
                .Where(i => hashes.Contains(i.ContentHash))
                .Select(i => new ImageRefDto(i.Pk, i.ContentHash))
                .ToListAsync();
        });

    public Task<Returning> InsertImageAsync(DocImage image, byte[] content) =>
        Returning.TryTask(async () =>
        {
            await using var db = await _factory.CreateDbContextAsync();

            // Metadata + binary in one SaveChanges (client-generated Pk makes this a single unit).
            db.Images.Add(image);
            db.ImageContents.Add(new DocImageContent
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

            await db.SaveChangesAsync();
            return Returning.Success();
        });

    public Task<ReturningList<ImageRefDto>> GetImageRefsAsync(IReadOnlyCollection<Guid> imagePks) =>
        ReturningList<ImageRefDto>.TryTask(async () =>
        {
            var pks = imagePks.ToList();

            await using var db = await _factory.CreateDbContextAsync();
            // Only Pk + ContentHash — the binary column is never materialized here.
            return await db.Images.AsNoTracking()
                .Where(i => pks.Contains(i.Pk))
                .Select(i => new ImageRefDto(i.Pk, i.ContentHash))
                .ToListAsync();
        });

    public Task<ReturningList<ImageBlobDto>> GetImageContentsAsync(IReadOnlyCollection<Guid> imagePks) =>
        ReturningList<ImageBlobDto>.TryTask(async () =>
        {
            var pks = imagePks.ToList();

            await using var db = await _factory.CreateDbContextAsync();
            // All requested binaries in ONE batch query.
            return await db.ImageContents.AsNoTracking()
                .Where(c => pks.Contains(c.Fk_DocImage))
                .Select(c => new ImageBlobDto(c.Fk_DocImage, c.Content))
                .ToListAsync();
        });

    public Task<Returning<ImageBlobDto?>> GetImageContentByHashAsync(string contentHash) =>
        Returning<ImageBlobDto?>.TryTask(async () =>
        {
            await using var db = await _factory.CreateDbContextAsync();
            return await db.ImageContents.AsNoTracking()
                .Where(c => db.Images.Any(i => i.Pk == c.Fk_DocImage && i.ContentHash == contentHash))
                .Select(c => new ImageBlobDto(c.Fk_DocImage, c.Content))
                .FirstOrDefaultAsync();
        });

    public Task<ReturningList<string>> GetAllImageHashesAsync() =>
        ReturningList<string>.TryTask(async () =>
        {
            await using var db = await _factory.CreateDbContextAsync();
            return await db.Images.AsNoTracking().Select(i => i.ContentHash).ToListAsync();
        });

    public Task<ReturningList<Guid>> FilterExistingImagePksAsync(IReadOnlyCollection<Guid> imagePks) =>
        ReturningList<Guid>.TryTask(async () =>
        {
            var pks = imagePks.ToList();

            await using var db = await _factory.CreateDbContextAsync();
            return await db.Images.AsNoTracking()
                .Where(i => pks.Contains(i.Pk))
                .Select(i => i.Pk)
                .ToListAsync();
        });

    // ---------------------------------------------------------------- Helpers

    /// <summary>
    /// Applies the visibility rule INSIDE the query (public OR any active permission granted
    /// to the filter). Comparison case-sensitivity follows the database collation.
    /// </summary>
    private static IQueryable<DocPage> ApplyVisibility(IQueryable<DocPage> query, VisibilityFilter filter)
    {
        if (filter.SeesEverything) return query;

        var permissions = filter.Permissions.ToList();
        return query.Where(p => p.IsPublic ||
            p.DocPagePermissions.Any(dp => dp.RowIsActive && permissions.Contains(dp.Permission)));
    }

    private static void Touch(EntityBase entity, AuditStamp audit)
    {
        entity.RowUpdateDate = audit.Timestamp;
        entity.RowUserUpdate = audit.UserName;
    }
}
