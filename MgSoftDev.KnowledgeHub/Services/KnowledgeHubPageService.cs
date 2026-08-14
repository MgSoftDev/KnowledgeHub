using System.Text;
using MgSoftDev.KnowledgeHub.Contracts;
using MgSoftDev.KnowledgeHub.Dtos;
using MgSoftDev.KnowledgeHub.Entities;
using MgSoftDev.KnowledgeHub.Enums;
using MgSoftDev.KnowledgeHub.Security;
using MgSoftDev.KnowledgeHub.Store;
using MgSoftDev.ReturningCore;
using MgSoftDev.ReturningCore.Exceptions;
using MgSoftDev.ReturningCore.Helper;
using MgSoftDev.ReturningCore.Logger;

namespace MgSoftDev.KnowledgeHub.Services;

/// <summary>
/// Core page service: permission-filtered tree, reading, editing, insert-only versioning,
/// atomic publishing with cross-machine conflict detection, page management, per-page
/// visibility and search. Business rules live here; persistence is delegated to
/// <see cref="IKnowledgeHubStore"/>. Every mutation is guarded server-side by the reserved
/// KnowledgeHub.* permissions (the UI merely mirrors those checks).
/// </summary>
public sealed class KnowledgeHubPageService : IKnowledgeHubPageService
{
    private const string NoPermissionMessage = "No tienes permiso para realizar esta acción";

    /// <summary>
    /// Deliberately ambiguous: it must NOT tell "does not exist" apart from "you may not see it",
    /// or the rejection itself becomes a way to enumerate pages by trying Guids.
    /// </summary>
    private const string NotVisibleMessage = "La página no existe o no tienes permiso para verla";

    private readonly IKnowledgeHubStore _store;
    private readonly IKnowledgeHubUserContext _user;
    private readonly IKnowledgeHubImageService _imageService;
    private readonly KnowledgeHubOptions _options;
    private readonly IKnowledgeHubHtmlSanitizer? _sanitizer;

    // sanitizer is OPTIONAL: absent unless the host registers one (see the
    // MgSoftDev.KnowledgeHub.HtmlSanitizer package). When null nothing is cleaned and saving
    // behaves exactly as before.
    public KnowledgeHubPageService(IKnowledgeHubStore store, IKnowledgeHubUserContext user,
        IKnowledgeHubImageService imageService, KnowledgeHubOptions options,
        IKnowledgeHubHtmlSanitizer? sanitizer = null)
    {
        _store = store;
        _user = user;
        _imageService = imageService;
        _options = options;
        _sanitizer = sanitizer;
    }

    #region Tree & reading

    public Task<ReturningList<PageTreeNodeDto>> GetTreeAsync() =>
        ReturningList<PageTreeNodeDto>.TryTask(async () =>
        {
            var pagesR = await _store.GetVisiblePagesAsync(_user.ToVisibilityFilter());
            if (!pagesR.Ok) pagesR.Throw();

            return BuildTree(pagesR.Value!);
        }, saveLog: true);

    /// <summary>
    /// Builds the tree from the visible set, applying inheritance: a node is shown only when its
    /// whole ancestor chain is also visible (parent not visible → children hidden).
    /// </summary>
    private static List<PageTreeNodeDto> BuildTree(List<PageTreeNodeDto> flat)
    {
        var byId = flat.ToDictionary(n => n.Pk);
        var included = new HashSet<Guid>();

        bool IsVisible(PageTreeNodeDto node)
        {
            if (included.Contains(node.Pk)) return true;
            if (node.Fk_DocPageParent is null) { included.Add(node.Pk); return true; }
            if (!byId.TryGetValue(node.Fk_DocPageParent.Value, out var parent)) return false;
            if (IsVisible(parent)) { included.Add(node.Pk); return true; }
            return false;
        }

        foreach (var node in flat) IsVisible(node);

        var visibleNodes = flat.Where(n => included.Contains(n.Pk))
                               .OrderBy(n => n.SortOrder).ThenBy(n => n.Title)
                               .ToList();
        var dict = visibleNodes.ToDictionary(n => n.Pk);
        var roots = new List<PageTreeNodeDto>();

        foreach (var node in visibleNodes)
        {
            if (node.Fk_DocPageParent is Guid pid && dict.TryGetValue(pid, out var parent))
                parent.Children.Add(node);
            else
                roots.Add(node);
        }
        return roots;
    }

    public Task<Returning<PageReadDto>> GetPageForReadAsync(Guid pagePk) =>
        Returning<PageReadDto>.TryTask(async () =>
        {
            var headerR = await _store.GetVisiblePageHeaderAsync(pagePk, _user.ToVisibilityFilter());
            if (!headerR.Ok) headerR.Throw();

            var header = headerR.Value;
            if (header is null)
                return Returning.Unfinished("La página no existe o no tienes permiso para verla", UnfinishedInfo.NotifyType.Warning);
            if (header.PublishedVersionPk is not Guid publishedPk)
                return Returning.Unfinished("Esta página aún no ha sido publicada", UnfinishedInfo.NotifyType.Warning);

            var versionR = await _store.GetVersionAsync(publishedPk);
            if (!versionR.Ok) versionR.Throw();

            if (versionR.Value is not { } version)
                return Returning.Unfinished("No se encontró la versión publicada", UnfinishedInfo.NotifyType.Warning);

            var read = ToReadDto(version);
            read.Icon = header.Icon;
            read.IconColor = header.IconColor;
            return read;
        }, saveLog: true);

    public Task<Returning<PageReadDto>> GetVersionContentAsync(Guid versionPk) =>
        Returning<PageReadDto>.TryTask(async () =>
        {
            // Reading an old version is an editing concern, and the version key bypasses the
            // visibility filter, so both gates are needed. Without them this returned the full html
            // of any version of any page — drafts that were never published included.
            if (!_user.CanEdit())
                return Returning.Unfinished(NoPermissionMessage, UnfinishedInfo.NotifyType.Warning);

            if (await EnsureVersionVisibleAsync(versionPk) is not { } version)
                return Returning.Unfinished(NotVisibleMessage, UnfinishedInfo.NotifyType.Warning);

            return ToReadDto(version);
        }, saveLog: true);

    public Task<Returning<PageEditDto>> GetPageForEditAsync(Guid pagePk) =>
        Returning<PageEditDto>.TryTask(async () =>
        {
            if (!_user.CanEdit())
                return Returning.Unfinished(NoPermissionMessage, UnfinishedInfo.NotifyType.Warning);
            // CanEdit says the user may edit SOMETHING, never that they may edit THIS.
            if (await EnsureVisibleAsync(pagePk) is null)
                return Returning.Unfinished(NotVisibleMessage, UnfinishedInfo.NotifyType.Warning);

            var pageR = await _store.GetPageAsync(pagePk);
            if (!pageR.Ok) pageR.Throw();
            if (pageR.Value is not { } page)
                return Returning.Unfinished("Página no encontrada", UnfinishedInfo.NotifyType.Warning);

            var latestR = await _store.GetLatestVersionAsync(pagePk);
            if (!latestR.Ok) latestR.Throw();
            var latest = latestR.Value;

            return new PageEditDto
            {
                PagePk = page.Pk,
                Slug = page.Slug,
                IsPublic = page.IsPublic,
                Icon = page.Icon,
                IconColor = page.IconColor,
                // Fall back to the page title for a brand-new page that has no versions yet.
                Title = latest?.Title ?? page.Title,
                ContentHtml = latest?.ContentHtml ?? string.Empty,
                BaseVersionNumber = latest?.VersionNumber ?? 0
            };
        }, saveLog: true);

    private static PageReadDto ToReadDto(PageVersionDto version) => new()
    {
        PagePk = version.PagePk,
        VersionPk = version.VersionPk,
        Title = version.Title,
        ContentHtml = version.ContentHtml,
        VersionNumber = version.VersionNumber,
        Status = version.Status,
        PublishedAt = version.PublishedAt
    };

    #endregion

    #region Versioning & publishing

    public Task<Returning<int>> SaveDraftAsync(PageEditDto draft) =>
        Returning<int>.TryTask(async () =>
        {
            if (!_user.CanEdit())
                return Returning.Unfinished(NoPermissionMessage, UnfinishedInfo.NotifyType.Warning);
            if (string.IsNullOrWhiteSpace(draft.Title))
                return Returning.Unfinished("El título es requerido", UnfinishedInfo.NotifyType.Warning);
            // Also the only place that checks the page EXISTS: without it a made-up Guid left orphan
            // versions behind in stores with no foreign keys.
            if (await EnsureVisibleAsync(draft.PagePk) is null)
                return Returning.Unfinished(NotVisibleMessage, UnfinishedInfo.NotifyType.Warning);

            // Replace any pasted data-URI images with uploaded docimg:// references before storing.
            // Refuse the save when one could not be processed: storing it would leave the base64
            // blob inline (and duplicated in every later version) without the user noticing.
            var (html, imageFailures) = await InterceptDataUrisAsync(draft.ContentHtml ?? string.Empty);
            if (imageFailures.Count > 0)
                return Returning.Unfinished(
                    $"No se pudieron procesar {imageFailures.Count} imagen(es) pegada(s): " +
                    $"{string.Join(", ", imageFailures.Distinct())}. " +
                    "Quítalas o conviértelas a PNG/JPG antes de guardar.",
                    UnfinishedInfo.NotifyType.Warning);

            // Existing images are shown in the editor as display URLs; turn them back into the
            // stable docimg:// references (matched by hash → same DocImage id, no duplication).
            html = await RewriteDisplayUrlsToDocImgAsync(html);

            // Last checkpoint before persisting, so markup typed by hand in the Source view is
            // checked too. It runs HERE —after both image rewrites— because only now is the html
            // in its canonical stored form (every image is docimg://, no host-specific URLs), and
            // still before GetExistingImagePksAsync below, so removing an <img> keeps the
            // page↔image links consistent with what actually gets stored.
            html = SanitizeForSave(html, draft.PagePk);

            var maxR = await _store.GetMaxVersionNumberAsync(draft.PagePk);
            if (!maxR.Ok) maxR.Throw();
            var newNumber = maxR.Value + 1;

            var version = new DocPageVersion
            {
                Fk_DocPage = draft.PagePk,
                VersionNumber = newNumber,
                Title = draft.Title,
                ContentHtml = html,
                Status = DocPageStatus.Draft,
                ChangeNote = draft.ChangeNote
            };
            EntityStamp.PrepareNew(version, _user.UserName, DateTime.Now);

            var imagePks = await GetExistingImagePksAsync(html);
            var insertR = await _store.InsertVersionAsync(version, imagePks);
            if (!insertR.Ok) insertR.Throw();

            return newNumber;
        }, saveLog: true);

    public Task<Returning> PublishAsync(Guid pagePk, int baseVersionNumber) =>
        Returning.TryTask(async () =>
        {
            if (!_user.CanPublish(_options))
                return Returning.Unfinished(NoPermissionMessage, UnfinishedInfo.NotifyType.Warning);
            if (await EnsureVisibleAsync(pagePk) is null)
                return Returning.Unfinished(NotVisibleMessage, UnfinishedInfo.NotifyType.Warning);

            var publishR = await _store.TryPublishAsync(pagePk, baseVersionNumber, Stamp());
            if (!publishR.Ok) publishR.Throw();

            return publishR.Value switch
            {
                PublishOutcome.Published => Returning.Success(),
                PublishOutcome.Conflict => Returning.Unfinished(
                    "Otro usuario publicó una versión más reciente. Recarga la página antes de publicar.",
                    UnfinishedInfo.NotifyType.Warning),
                PublishOutcome.PageNotFound => Returning.Unfinished("Página no encontrada", UnfinishedInfo.NotifyType.Warning),
                _ => Returning.Unfinished("No hay ninguna versión para publicar", UnfinishedInfo.NotifyType.Warning)
            };
        }, saveLog: true);

    public Task<Returning> RestoreVersionAsync(Guid versionPk) =>
        Returning.TryTask(async () =>
        {
            if (!_user.CanEdit())
                return Returning.Unfinished(NoPermissionMessage, UnfinishedInfo.NotifyType.Warning);

            if (await EnsureVersionVisibleAsync(versionPk) is not { } old)
                return Returning.Unfinished(NotVisibleMessage, UnfinishedInfo.NotifyType.Warning);

            var maxR = await _store.GetMaxVersionNumberAsync(old.PagePk);
            if (!maxR.Ok) maxR.Throw();

            var version = new DocPageVersion
            {
                Fk_DocPage = old.PagePk,
                VersionNumber = maxR.Value + 1,
                Title = old.Title,
                ContentHtml = old.ContentHtml,
                Status = DocPageStatus.Draft,
                ChangeNote = $"Restaurado desde la versión {old.VersionNumber}"
            };
            EntityStamp.PrepareNew(version, _user.UserName, DateTime.Now);

            var imagePks = await GetExistingImagePksAsync(old.ContentHtml);
            var insertR = await _store.InsertVersionAsync(version, imagePks);
            if (!insertR.Ok) insertR.Throw();

            return Returning.Success();
        }, saveLog: true);

    public Task<ReturningList<VersionListItemDto>> GetVersionsAsync(Guid pagePk) =>
        ReturningList<VersionListItemDto>.TryTask(async () =>
        {
            // The list of versions is what turns a page Guid into version Guids, so leaving it open
            // handed out the keys to GetVersionContentAsync.
            if (!_user.CanEdit())
                return Returning.Unfinished(NoPermissionMessage, UnfinishedInfo.NotifyType.Warning);
            if (await EnsureVisibleAsync(pagePk) is null)
                return Returning.Unfinished(NotVisibleMessage, UnfinishedInfo.NotifyType.Warning);

            var pageR = await _store.GetPageAsync(pagePk);
            if (!pageR.Ok) pageR.Throw();
            var publishedPk = pageR.Value?.Fk_DocPageVersionPublished;

            var listR = await _store.GetVersionListAsync(pagePk);
            if (!listR.Ok) listR.Throw();

            var list = listR.Value!;
            if (publishedPk is Guid published)
                foreach (var item in list)
                    item.IsCurrentPublished = item.Pk == published;
            return list;
        }, saveLog: true);

    #endregion

    #region Page management

    public Task<Returning<PageInfoDto>> GetPageInfoAsync(Guid pagePk) =>
        Returning<PageInfoDto>.TryTask(async () =>
        {
            if (await EnsureVisibleAsync(pagePk) is null)
                return Returning.Unfinished(NotVisibleMessage, UnfinishedInfo.NotifyType.Warning);

            var pageR = await _store.GetPageAsync(pagePk);
            if (!pageR.Ok) pageR.Throw();
            if (pageR.Value is not { } page)
                return Returning.Unfinished("Página no encontrada", UnfinishedInfo.NotifyType.Warning);

            return new PageInfoDto
            {
                Pk = page.Pk,
                Title = page.Title,
                Slug = page.Slug,
                Fk_DocPageParent = page.Fk_DocPageParent,
                SortOrder = page.SortOrder,
                Icon = page.Icon,
                IconColor = page.IconColor,
                ExcludeFromPdf = page.ExcludeFromPdf
            };
        }, saveLog: true);

    public Task<Returning<Guid>> CreatePageAsync(Guid? parentPk, string title, string? slug = null) =>
        Returning<Guid>.TryTask(async () =>
        {
            if (!_user.CanEdit())
                return Returning.Unfinished(NoPermissionMessage, UnfinishedInfo.NotifyType.Warning);
            if (string.IsNullOrWhiteSpace(title))
                return Returning.Unfinished("El título es requerido", UnfinishedInfo.NotifyType.Warning);
            // The parent was never validated at all — not even that it existed.
            if (parentPk is Guid parent && await EnsureVisibleAsync(parent) is null)
                return Returning.Unfinished("La página superior no existe o no tienes permiso para verla",
                    UnfinishedInfo.NotifyType.Warning);

            var uniqueSlugR = await ResolveFreeSlugAsync(
                string.IsNullOrWhiteSpace(slug) ? KnowledgeHubSlug.Slugify(title) : slug);
            if (!uniqueSlugR.OkNotNull) uniqueSlugR.Throw();

            var maxSortR = await _store.GetMaxSortOrderAsync(parentPk);
            if (!maxSortR.Ok) maxSortR.Throw();

            var page = new DocPage
            {
                Fk_DocPageParent = parentPk,
                Slug = uniqueSlugR.Value,
                Title = title,
                SortOrder = maxSortR.Value + 1,
                IsPublic = false
            };
            EntityStamp.PrepareNew(page, _user.UserName, DateTime.Now);

            var insertR = await _store.InsertPageAsync(page);
            if (!insertR.Ok) insertR.Throw();

            // A subpage inherits its parent's audience: a page under "Producción" belongs to
            // Producción, which is what everyone expects and what stops the new page from being
            // invisible to the very person who just created it. Two writes, not one transaction —
            // if this second one fails the page is left UNCONFIGURED, which the visibility rule
            // still shows to whoever may edit, so nothing is lost.
            if (parentPk is Guid inheritFrom)
            {
                var parentPermsR = await _store.GetPagePermissionsAsync(inheritFrom);
                if (!parentPermsR.Ok) parentPermsR.Throw();
                if (parentPermsR.Value is { } parentPerms &&
                    (parentPerms.IsPublic || parentPerms.Permissions.Count > 0))
                {
                    var applyR = await _store.SetPagePermissionsAsync(
                        page.Pk, parentPerms.IsPublic, parentPerms.Permissions, Stamp());
                    if (!applyR.Ok) applyR.Throw();
                }
            }

            // MAX+1 can leave a gap (deleted siblings still count towards the max), so collapse
            // the group to 1..N; the new page keeps the last position.
            await NormalizeSiblingsAsync(await LoadLinksAsync(), parentPk, pinLastPk: page.Pk);

            return page.Pk;
        }, saveLog: true);

    public Task<Returning> RenamePageAsync(Guid pagePk, string title) =>
        Returning.TryTask(async () =>
        {
            if (!_user.CanEdit())
                return Returning.Unfinished(NoPermissionMessage, UnfinishedInfo.NotifyType.Warning);
            if (string.IsNullOrWhiteSpace(title))
                return Returning.Unfinished("El título es requerido", UnfinishedInfo.NotifyType.Warning);
            if (await EnsureVisibleAsync(pagePk) is null)
                return Returning.Unfinished(NotVisibleMessage, UnfinishedInfo.NotifyType.Warning);

            var okR = await _store.RenamePageAsync(pagePk, title, Stamp());
            if (!okR.Ok) okR.Throw();
            if (!okR.Value)
                return Returning.Unfinished("Página no encontrada", UnfinishedInfo.NotifyType.Warning);
            return Returning.Success();
        }, saveLog: true);

    public Task<Returning> MovePageAsync(Guid pagePk, Guid? newParentPk) =>
        Returning.TryTask(async () =>
        {
            if (!_user.CanEdit())
                return Returning.Unfinished(NoPermissionMessage, UnfinishedInfo.NotifyType.Warning);
            if (newParentPk == pagePk)
                return Returning.Unfinished("Una página no puede ser su propio padre", UnfinishedInfo.NotifyType.Warning);
            // BOTH ends: moving a page you can see UNDER a parent you cannot would park content
            // inside a branch that does not exist for you.
            if (await EnsureVisibleAsync(pagePk) is null)
                return Returning.Unfinished(NotVisibleMessage, UnfinishedInfo.NotifyType.Warning);
            if (newParentPk is Guid destination && await EnsureVisibleAsync(destination) is null)
                return Returning.Unfinished("La página destino no existe o no tienes permiso para verla",
                    UnfinishedInfo.NotifyType.Warning);

            var linksBefore = await LoadLinksAsync();
            var current = linksBefore.FirstOrDefault(l => l.Pk == pagePk);
            if (current is null)
                return Returning.Unfinished("Página no encontrada", UnfinishedInfo.NotifyType.Warning);

            var oldParentPk = current.ParentPk;

            // Reject moving a page under one of its own descendants (would create a cycle).
            if (newParentPk is Guid target)
            {
                var parents = linksBefore.ToDictionary(l => l.Pk, l => l.ParentPk);

                var cursor = (Guid?)target;
                while (cursor is Guid node)
                {
                    if (node == pagePk)
                        return Returning.Unfinished("No puedes mover una página dentro de uno de sus descendientes", UnfinishedInfo.NotifyType.Warning);
                    cursor = parents.TryGetValue(node, out var parentPk) ? parentPk : null;
                }
            }

            var okR = await _store.MovePageAsync(pagePk, newParentPk, Stamp());
            if (!okR.Ok) okR.Throw();
            if (!okR.Value)
                return Returning.Unfinished("Página no encontrada", UnfinishedInfo.NotifyType.Warning);

            // The page kept the SortOrder it had under its previous parent, which is meaningless
            // here: land it LAST among its new siblings and close the gap it left behind.
            if (oldParentPk != newParentPk)
            {
                var linksAfter = await LoadLinksAsync();
                await NormalizeSiblingsAsync(linksAfter, newParentPk, pinLastPk: pagePk);
                await NormalizeSiblingsAsync(linksAfter, oldParentPk);
            }

            return Returning.Success();
        }, saveLog: true);

    public Task<Returning> ReorderAsync(Guid pagePk, int sortOrder) =>
        Returning.TryTask(async () =>
        {
            if (!_user.CanEdit())
                return Returning.Unfinished(NoPermissionMessage, UnfinishedInfo.NotifyType.Warning);

            if (await EnsureVisibleAsync(pagePk) is null)
                return Returning.Unfinished(NotVisibleMessage, UnfinishedInfo.NotifyType.Warning);

            var okR = await _store.SetSortOrderAsync(pagePk, sortOrder, Stamp());
            if (!okR.Ok) okR.Throw();
            if (!okR.Value)
                return Returning.Unfinished("Página no encontrada", UnfinishedInfo.NotifyType.Warning);

            // Collapse whatever number the caller picked back into a clean 1..N group.
            var links = await LoadLinksAsync();
            if (links.FirstOrDefault(l => l.Pk == pagePk) is { } page)
                await NormalizeSiblingsAsync(links, page.ParentPk);

            return Returning.Success();
        }, saveLog: true);

    public Task<Returning> MovePageOrderAsync(Guid pagePk, PageMoveDirection direction) =>
        Returning.TryTask(async () =>
        {
            if (!_user.CanEdit())
                return Returning.Unfinished(NoPermissionMessage, UnfinishedInfo.NotifyType.Warning);

            if (await EnsureVisibleAsync(pagePk) is null)
                return Returning.Unfinished(NotVisibleMessage, UnfinishedInfo.NotifyType.Warning);

            var linksR = await _store.GetActivePageLinksAsync();
            if (!linksR.Ok) linksR.Throw();
            var links = linksR.Value!;

            var page = links.FirstOrDefault(l => l.Pk == pagePk);
            if (page is null)
                return Returning.Unfinished("Página no encontrada", UnfinishedInfo.NotifyType.Warning);

            var siblings = SiblingsInDisplayOrder(links, page.ParentPk);
            var index = siblings.FindIndex(l => l.Pk == pagePk);
            var targetIndex = direction == PageMoveDirection.Up ? index - 1 : index + 1;

            // Already at the top/bottom: nothing to do, and it is not an error — the UI disables
            // the button there anyway.
            if (targetIndex < 0 || targetIndex >= siblings.Count) return Returning.Success();

            (siblings[index], siblings[targetIndex]) = (siblings[targetIndex], siblings[index]);

            var orders = siblings.Select((l, i) => new PageSortOrderDto(l.Pk, i + 1)).ToList();
            var writeR = await _store.SetSortOrdersAsync(orders, Stamp());
            if (!writeR.Ok) writeR.Throw();

            return Returning.Success();
        }, saveLog: true);

    public Task<Returning<int>> NormalizeAllPageOrdersAsync() =>
        Returning<int>.TryTask(async () =>
        {
            if (!_user.IsAdmin())
                return Returning.Unfinished("Solo un administrador puede normalizar el orden",
                    UnfinishedInfo.NotifyType.Warning);

            var linksR = await _store.GetActivePageLinksAsync();
            if (!linksR.Ok) linksR.Throw();
            var links = linksR.Value!;

            // Every sibling group of the tree, roots included, in one batch write.
            var orders = new List<PageSortOrderDto>();
            foreach (var group in links.GroupBy(l => l.ParentPk))
                orders.AddRange(SiblingsInDisplayOrder(links, group.Key)
                    .Select((l, i) => new PageSortOrderDto(l.Pk, i + 1)));

            var writeR = await _store.SetSortOrdersAsync(orders, Stamp());
            if (!writeR.Ok) writeR.Throw();

            return writeR.Value;
        }, saveLog: true);

    public Task<Returning> SetPageIconAsync(Guid pagePk, string? icon, string? iconColor) =>
        Returning.TryTask(async () =>
        {
            if (!_user.CanEdit())
                return Returning.Unfinished(NoPermissionMessage, UnfinishedInfo.NotifyType.Warning);

            if (await EnsureVisibleAsync(pagePk) is null)
                return Returning.Unfinished(NotVisibleMessage, UnfinishedInfo.NotifyType.Warning);

            var okR = await _store.SetPageIconAsync(pagePk, Normalize(icon), Normalize(iconColor), Stamp());
            if (!okR.Ok) okR.Throw();
            if (!okR.Value)
                return Returning.Unfinished("Página no encontrada", UnfinishedInfo.NotifyType.Warning);
            return Returning.Success();

            static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }, saveLog: true);

    public Task<Returning> SetPageExcludeFromPdfAsync(Guid pagePk, bool excludeFromPdf) =>
        Returning.TryTask(async () =>
        {
            if (!_user.CanEdit())
                return Returning.Unfinished(NoPermissionMessage, UnfinishedInfo.NotifyType.Warning);

            if (await EnsureVisibleAsync(pagePk) is null)
                return Returning.Unfinished(NotVisibleMessage, UnfinishedInfo.NotifyType.Warning);

            var okR = await _store.SetPageExcludeFromPdfAsync(pagePk, excludeFromPdf, Stamp());
            if (!okR.Ok) okR.Throw();
            if (!okR.Value)
                return Returning.Unfinished("Página no encontrada", UnfinishedInfo.NotifyType.Warning);
            return Returning.Success();
        }, saveLog: true);

    public Task<Returning> DeletePageAsync(Guid pagePk) =>
        Returning.TryTask(async () =>
        {
            if (!_user.CanEdit())
                return Returning.Unfinished(NoPermissionMessage, UnfinishedInfo.NotifyType.Warning);

            // Deleting takes the WHOLE subtree down. This gate covers the page itself; the subtree
            // is checked below, once it is known.
            if (await EnsureVisibleAsync(pagePk) is null)
                return Returning.Unfinished(NotVisibleMessage, UnfinishedInfo.NotifyType.Warning);

            var linksR = await _store.GetActivePageLinksAsync();
            if (!linksR.Ok) linksR.Throw();
            var links = linksR.Value!;

            // Collect the page and its whole subtree for a consistent soft delete.
            var toDelete = new HashSet<Guid> { pagePk };
            bool added;
            do
            {
                added = false;
                foreach (var link in links)
                    if (link.ParentPk is Guid parent && toDelete.Contains(parent) && toDelete.Add(link.Pk))
                        added = true;
            } while (added);

            // The subtree came from the UNFILTERED link list —it has to, or the cascade would leave
            // orphans— which is precisely why it has to be checked against what this user can see.
            // Without this an editor deleting a branch destroys restricted pages underneath it
            // silently: pages they cannot see, cannot list, and are never told about.
            var hidden = await CountHiddenAsync(toDelete);
            if (hidden > 0)
            {
                var title = links.FirstOrDefault(l => l.Pk == pagePk)?.Title;
                return Returning.Unfinished(
                    title is null ? "No se puede eliminar la página" : $"No se puede eliminar «{title}»",
                    hidden == 1
                        ? "Contiene 1 subpágina que no tienes permiso para ver. Pide a un administrador " +
                          "que la elimine o que te dé acceso a ella."
                        : $"Contiene {hidden} subpáginas que no tienes permiso para ver. Pide a un " +
                          "administrador que las elimine o que te dé acceso a ellas.",
                    UnfinishedInfo.NotifyType.Warning);
            }

            // Remember the parent BEFORE deleting: afterwards the row is no longer active and
            // would not come back in the links.
            var parentPk = links.FirstOrDefault(l => l.Pk == pagePk)?.ParentPk;

            var countR = await _store.SoftDeletePagesAsync(toDelete, Stamp());
            if (!countR.Ok) countR.Throw();
            if (countR.Value == 0)
                return Returning.Unfinished("Página no encontrada", UnfinishedInfo.NotifyType.Warning);

            // Close the gap the deleted page left among its siblings.
            await NormalizeSiblingsAsync(await LoadLinksAsync(), parentPk);

            return Returning.Success();
        }, saveLog: true);

    #endregion

    #region Permissions & search

    public Task<Returning<PagePermissionsDto>> GetPermissionsAsync(Guid pagePk) =>
        Returning<PagePermissionsDto>.TryTask(async () =>
        {
            if (!_user.CanManagePermissions(_options))
                return Returning.Unfinished(NoPermissionMessage, UnfinishedInfo.NotifyType.Warning);
            // Reading the ACL of a page you cannot see is the reconnaissance step for the escalation:
            // it tells you exactly which permission to grant yourself.
            if (await EnsureVisibleAsync(pagePk) is null)
                return Returning.Unfinished(NotVisibleMessage, UnfinishedInfo.NotifyType.Warning);

            var permsR = await _store.GetPagePermissionsAsync(pagePk);
            if (!permsR.Ok) permsR.Throw();
            if (permsR.Value is not { } perms)
                return Returning.Unfinished("Página no encontrada", UnfinishedInfo.NotifyType.Warning);
            return perms;
        }, saveLog: true);

    public Task<Returning> SetPermissionsAsync(Guid pagePk, bool isPublic, IReadOnlyList<string> permissions) =>
        Returning.TryTask(async () =>
        {
            if (!_user.CanManagePermissions(_options))
                return Returning.Unfinished(NoPermissionMessage, UnfinishedInfo.NotifyType.Warning);

            // PRIVILEGE ESCALATION, closed here. CanManagePermissions falls back to CanEdit unless
            // the host opts in, so without this any editor could set IsPublic on a branch they
            // cannot see and then read it through the front door. Granting yourself access to
            // something you cannot see must not be possible.
            //
            // An UNCONFIGURED page does pass, and that is the point: it is visible to every editor
            // already, so configuring it grants nothing new — it is how a new page gets its audience.
            if (await EnsureVisibleAsync(pagePk) is null)
                return Returning.Unfinished(NotVisibleMessage, UnfinishedInfo.NotifyType.Warning);

            var distinct = permissions
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var okR = await _store.SetPagePermissionsAsync(pagePk, isPublic, distinct, Stamp());
            if (!okR.Ok) okR.Throw();
            if (!okR.Value)
                return Returning.Unfinished("Página no encontrada", UnfinishedInfo.NotifyType.Warning);
            return Returning.Success();
        }, saveLog: true);

    public Task<ReturningList<SearchResultDto>> SearchAsync(string term) =>
        ReturningList<SearchResultDto>.TryTask(async () =>
        {
            if (string.IsNullOrWhiteSpace(term))
                return new List<SearchResultDto>();

            var candidatesR = await _store.SearchPublishedAsync(term, _user.ToVisibilityFilter());
            if (!candidatesR.Ok) candidatesR.Throw();

            return candidatesR.Value!
                .Select(x => new SearchResultDto
                {
                    PagePk = x.PagePk,
                    Title = x.Title,
                    Slug = x.Slug,
                    Snippet = BuildSnippet(x.ContentHtml, term),
                    Icon = x.Icon,
                    IconColor = x.IconColor
                })
                .ToList();
        }, saveLog: true);

    #endregion

    #region Helpers

    private AuditStamp Stamp() => new(_user.UserName, DateTime.Now);

    /// <summary>
    /// Children of <paramref name="parentPk"/> in the SAME order the tree shows them
    /// (see BuildTree), so renumbering never reshuffles what the user is looking at.
    /// </summary>
    private static List<PageLinkDto> SiblingsInDisplayOrder(IReadOnlyList<PageLinkDto> links, Guid? parentPk) =>
        links.Where(l => l.ParentPk == parentPk)
             .OrderBy(l => l.SortOrder)
             .ThenBy(l => l.Title)
             .ToList();

    /// <summary>
    /// Renumbers one sibling group to 1..N. This is what keeps positions meaningful: deleting or
    /// moving pages away used to leave gaps (a 5th child holding SortOrder 15), which made the
    /// numbers useless to reason about.
    /// </summary>
    /// <param name="links">Already-loaded structural rows, to avoid re-reading them.</param>
    /// <param name="parentPk">Group to renumber; null means the root level.</param>
    /// <param name="pinLastPk">Optional page forced to the end (a page that just arrived here).</param>
    private async Task NormalizeSiblingsAsync(IReadOnlyList<PageLinkDto> links, Guid? parentPk,
        Guid? pinLastPk = null)
    {
        var siblings = SiblingsInDisplayOrder(links, parentPk);
        if (siblings.Count == 0) return;

        if (pinLastPk is Guid pinned && siblings.FirstOrDefault(l => l.Pk == pinned) is { } moved)
        {
            siblings.Remove(moved);
            siblings.Add(moved);
        }

        var orders = siblings.Select((l, i) => new PageSortOrderDto(l.Pk, i + 1)).ToList();
        var writeR = await _store.SetSortOrdersAsync(orders, Stamp());
        if (!writeR.Ok) writeR.Throw();
    }

    /// <summary>
    /// Finds a free slug, appending -2, -3… to the base when needed. Creating a page must NEVER
    /// fail over a name clash: two pages with the same title under different parents are perfectly
    /// legitimate, and the slug is an invisible identifier nothing looks up.
    ///
    /// The uniqueness is GLOBAL because that is what the unique index of every provider enforces —
    /// keeping it that way means existing databases need no migration. Deleted pages keep their
    /// slug reserved (<c>SlugExistsAsync</c> ignores RowIsActive on purpose, so the index stays
    /// valid); it just costs the reused title a suffix instead of blocking it as it used to.
    /// </summary>
    private async Task<Returning<string>> ResolveFreeSlugAsync(string baseSlug)
    {
        // Enough that no realistic tree reaches it, low enough to never spin: past it, a random
        // suffix ends the search in a single extra round trip.
        const int maxAttempts = 100;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            var candidate = attempt == 1 ? baseSlug : $"{baseSlug}-{attempt}";
            var existsR = await _store.SlugExistsAsync(candidate);
            if (!existsR.Ok) existsR.Throw();
            if (!existsR.Value) return candidate;
        }

        var random = Guid.NewGuid().ToString("n")[..8];
        return $"{baseSlug}-{random}";
    }

    /// <summary>
    /// The visibility gate for every entry point that takes a page Guid. Returns null when the user
    /// may not see the page — caller answers <see cref="NotVisibleMessage"/>.
    ///
    /// It exists because the store can only filter in the three methods that take a
    /// <see cref="VisibilityFilter"/> (visible pages, visible header, search). Every other store
    /// method answers by primary key with no notion of who is asking, so a Guid obtained anywhere —
    /// a link in another page's html, a bookmark, a log — would otherwise be enough to read or
    /// change a page the user cannot see. Holding a Guid is not permission to use it.
    ///
    /// Admins pass through: <c>ToVisibilityFilter()</c> already returns the admin filter.
    /// </summary>
    private async Task<PageHeaderDto?> EnsureVisibleAsync(Guid pagePk)
    {
        var headerR = await _store.GetVisiblePageHeaderAsync(pagePk, _user.ToVisibilityFilter());
        if (!headerR.Ok) headerR.Throw();
        return headerR.Value;
    }

    /// <summary>
    /// How many of these pages the user cannot see. For cascades: the subtree is necessarily built
    /// from the UNFILTERED link list, so the only way to know whether an operation would reach
    /// content the user has no business touching is to compare it against the visible set.
    /// Admins short-circuit without asking the store, so the ordinary case pays nothing.
    /// </summary>
    private async Task<int> CountHiddenAsync(IReadOnlyCollection<Guid> pagePks)
    {
        var filter = _user.ToVisibilityFilter();
        if (filter.SeesEverything || pagePks.Count == 0) return 0;

        var visibleR = await _store.GetVisiblePagesAsync(filter);
        if (!visibleR.Ok) visibleR.Throw();

        var visible = visibleR.Value!.Select(p => p.Pk).ToHashSet();
        return pagePks.Count(pk => !visible.Contains(pk));
    }

    /// <summary>
    /// Same gate, keyed by version. A <c>versionPk</c> SKIPS the filter by design — no store method
    /// resolves a version against a visibility filter — so it has to be turned into its page and
    /// checked. Costs one extra round trip, and that is the price of the key being the version.
    /// Returns the version only when its page is visible.
    /// </summary>
    private async Task<PageVersionDto?> EnsureVersionVisibleAsync(Guid versionPk)
    {
        var versionR = await _store.GetVersionAsync(versionPk);
        if (!versionR.Ok) versionR.Throw();
        if (versionR.Value is not { } version) return null;

        return await EnsureVisibleAsync(version.PagePk) is null ? null : version;
    }

    /// <summary>Reloads the structural rows, for callers that need them after a write.</summary>
    private async Task<IReadOnlyList<PageLinkDto>> LoadLinksAsync()
    {
        var linksR = await _store.GetActivePageLinksAsync();
        if (!linksR.Ok) linksR.Throw();
        return linksR.Value!;
    }

    /// <summary>
    /// Cleans the html right before it is stored, when a sanitizer is registered. Removing content
    /// silently would be rude, but interrupting the save would be worse, so a change is recorded in
    /// the log instead of surfacing to the user. The sanitizer pass is idempotent, so a document
    /// that is already clean logs nothing on later saves.
    /// </summary>
    private string SanitizeForSave(string html, Guid pagePk)
    {
        if (_sanitizer is null || string.IsNullOrEmpty(html)) return html;

        var clean = _sanitizer.Sanitize(html, HtmlSanitizeContext.Save);
        if (clean == html) return html;

        if (ReturningLogger.LoggerService is not null)
            new UnfinishedInfo(
                "HTML saneado al guardar",
                $"Se limpió el contenido de la página {pagePk} antes de almacenarlo " +
                $"({html.Length} → {clean.Length} caracteres). Usuario: {_user.UserName}.",
                UnfinishedInfo.NotifyType.Information)
                .SaveLog(this, nameof(KnowledgeHubPageService));

        return clean;
    }

    /// <summary>
    /// Uploads every pasted data-URI image and rewrites its src to a stable docimg:// reference.
    /// Returns the rewritten html plus the mime types that could NOT be processed: leaving a failed
    /// data-URI inline would persist the base64 blob inside ContentHtml —and, versioning being
    /// insert-only, duplicate it in every later version— so the caller must refuse the save
    /// instead of storing it silently.
    /// </summary>
    private async Task<(string Html, List<string> Failures)> InterceptDataUrisAsync(string html)
    {
        var failures = new List<string>();
        var matches = KnowledgeHubHtml.DataUriRegex().Matches(html);
        if (matches.Count == 0) return (html, failures);

        var sb = new StringBuilder(html);
        // Replace from last to first so earlier match indices stay valid.
        foreach (var match in matches.OrderByDescending(m => m.Index))
        {
            var mime = match.Groups["mime"].Value;

            byte[] bytes;
            try { bytes = Convert.FromBase64String(match.Groups["data"].Value); }
            catch { failures.Add(mime); continue; }

            var uploaded = await _imageService.UploadOrReplaceAsync(bytes, "pasted.webp");
            if (!uploaded.Ok)
            {
                // Business rejection (unsupported format…) is reported to the caller; a genuine
                // infrastructure failure (store/db) must propagate instead of being swallowed.
                if (uploaded.UnfinishedInfo is null) uploaded.Throw();
                failures.Add(mime);
                continue;
            }

            sb.Remove(match.Index, match.Length);
            sb.Insert(match.Index, KnowledgeHubHtml.DocImgUrl(uploaded.Value));
        }
        return (sb.ToString(), failures);
    }

    /// <summary>
    /// Reverses the reader/editor rewrite: turns display URLs (any base, always ending in
    /// <c>{hash}.webp</c>) back into <c>docimg://{pk}</c> by looking each image up by its content
    /// hash. This keeps the stored HTML free of host-specific URLs and preserves the original
    /// image id (no duplication).
    /// </summary>
    private async Task<string> RewriteDisplayUrlsToDocImgAsync(string html)
    {
        var matches = KnowledgeHubHtml.DisplayUrlRegex().Matches(html);
        if (matches.Count == 0) return html;

        var hashes = matches.Select(m => m.Groups["hash"].Value.ToLowerInvariant()).Distinct().ToList();
        var refsR = await _store.GetImageRefsByHashesAsync(hashes);
        if (!refsR.Ok) refsR.Throw();
        var byHash = refsR.Value!.ToDictionary(r => r.ContentHash, r => r.Pk, StringComparer.OrdinalIgnoreCase);

        return KnowledgeHubHtml.DisplayUrlRegex().Replace(html, m =>
        {
            var hash = m.Groups["hash"].Value.ToLowerInvariant();
            return byHash.TryGetValue(hash, out var pk) ? KnowledgeHubHtml.DocImgUrl(pk) : m.Value;
        });
    }

    /// <summary>
    /// DocImage pks referenced by the HTML that actually exist in the store, so a stray
    /// reference never breaks the page↔image link sync.
    /// </summary>
    private async Task<IReadOnlyList<Guid>> GetExistingImagePksAsync(string html)
    {
        var referenced = KnowledgeHubHtml.ExtractDocImagePks(html);
        if (referenced.Count == 0) return Array.Empty<Guid>();

        var validR = await _store.FilterExistingImagePksAsync(referenced.ToList());
        if (!validR.Ok) validR.Throw();
        return validR.Value!;
    }

    private static string BuildSnippet(string? html, string term)
    {
        if (string.IsNullOrEmpty(html)) return string.Empty;
        var text = KnowledgeHubHtml.HtmlTagRegex().Replace(html, " ").Trim();
        var idx = text.IndexOf(term, StringComparison.OrdinalIgnoreCase);
        if (idx < 0) return text.Length <= 160 ? text : text[..160] + "…";

        var start = Math.Max(0, idx - 60);
        var length = Math.Min(text.Length - start, 160);
        var snippet = text.Substring(start, length).Trim();
        return (start > 0 ? "…" : string.Empty) + snippet + (start + length < text.Length ? "…" : string.Empty);
    }

    #endregion
}
