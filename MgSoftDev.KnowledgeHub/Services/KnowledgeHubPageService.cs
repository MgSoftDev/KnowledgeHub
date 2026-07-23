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

    private readonly IKnowledgeHubStore _store;
    private readonly IKnowledgeHubUserContext _user;
    private readonly IKnowledgeHubImageService _imageService;
    private readonly KnowledgeHubOptions _options;

    public KnowledgeHubPageService(IKnowledgeHubStore store, IKnowledgeHubUserContext user,
        IKnowledgeHubImageService imageService, KnowledgeHubOptions options)
    {
        _store = store;
        _user = user;
        _imageService = imageService;
        _options = options;
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
            var versionR = await _store.GetVersionAsync(versionPk);
            if (!versionR.Ok) versionR.Throw();

            if (versionR.Value is not { } version)
                return Returning.Unfinished("Versión no encontrada", UnfinishedInfo.NotifyType.Warning);
            return ToReadDto(version);
        }, saveLog: true);

    public Task<Returning<PageEditDto>> GetPageForEditAsync(Guid pagePk) =>
        Returning<PageEditDto>.TryTask(async () =>
        {
            if (!_user.CanEdit())
                return Returning.Unfinished(NoPermissionMessage, UnfinishedInfo.NotifyType.Warning);

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

            // Replace any pasted data-URI images with uploaded docimg:// references before storing.
            var html = await InterceptDataUrisAsync(draft.ContentHtml ?? string.Empty);

            // Existing images are shown in the editor as display URLs; turn them back into the
            // stable docimg:// references (matched by hash → same DocImage id, no duplication).
            html = await RewriteDisplayUrlsToDocImgAsync(html);

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

            var oldR = await _store.GetVersionAsync(versionPk);
            if (!oldR.Ok) oldR.Throw();
            if (oldR.Value is not { } old)
                return Returning.Unfinished("Versión no encontrada", UnfinishedInfo.NotifyType.Warning);

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
                IconColor = page.IconColor
            };
        }, saveLog: true);

    public Task<Returning<Guid>> CreatePageAsync(Guid? parentPk, string title, string slug) =>
        Returning<Guid>.TryTask(async () =>
        {
            if (!_user.CanEdit())
                return Returning.Unfinished(NoPermissionMessage, UnfinishedInfo.NotifyType.Warning);
            if (string.IsNullOrWhiteSpace(title))
                return Returning.Unfinished("El título es requerido", UnfinishedInfo.NotifyType.Warning);
            if (string.IsNullOrWhiteSpace(slug))
                return Returning.Unfinished("El slug es requerido", UnfinishedInfo.NotifyType.Warning);

            var existsR = await _store.SlugExistsAsync(slug);
            if (!existsR.Ok) existsR.Throw();
            if (existsR.Value)
                return Returning.Unfinished("Ya existe una página con ese slug", UnfinishedInfo.NotifyType.Warning);

            var maxSortR = await _store.GetMaxSortOrderAsync(parentPk);
            if (!maxSortR.Ok) maxSortR.Throw();

            var page = new DocPage
            {
                Fk_DocPageParent = parentPk,
                Slug = slug,
                Title = title,
                SortOrder = maxSortR.Value + 1,
                IsPublic = false
            };
            EntityStamp.PrepareNew(page, _user.UserName, DateTime.Now);

            var insertR = await _store.InsertPageAsync(page);
            if (!insertR.Ok) insertR.Throw();

            return page.Pk;
        }, saveLog: true);

    public Task<Returning> RenamePageAsync(Guid pagePk, string title) =>
        Returning.TryTask(async () =>
        {
            if (!_user.CanEdit())
                return Returning.Unfinished(NoPermissionMessage, UnfinishedInfo.NotifyType.Warning);
            if (string.IsNullOrWhiteSpace(title))
                return Returning.Unfinished("El título es requerido", UnfinishedInfo.NotifyType.Warning);

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

            // Reject moving a page under one of its own descendants (would create a cycle).
            if (newParentPk is Guid target)
            {
                var linksR = await _store.GetActivePageLinksAsync();
                if (!linksR.Ok) linksR.Throw();
                var parents = linksR.Value!.ToDictionary(l => l.Pk, l => l.ParentPk);

                var cursor = (Guid?)target;
                while (cursor is Guid current)
                {
                    if (current == pagePk)
                        return Returning.Unfinished("No puedes mover una página dentro de uno de sus descendientes", UnfinishedInfo.NotifyType.Warning);
                    cursor = parents.TryGetValue(current, out var parentPk) ? parentPk : null;
                }
            }

            var okR = await _store.MovePageAsync(pagePk, newParentPk, Stamp());
            if (!okR.Ok) okR.Throw();
            if (!okR.Value)
                return Returning.Unfinished("Página no encontrada", UnfinishedInfo.NotifyType.Warning);
            return Returning.Success();
        }, saveLog: true);

    public Task<Returning> ReorderAsync(Guid pagePk, int sortOrder) =>
        Returning.TryTask(async () =>
        {
            if (!_user.CanEdit())
                return Returning.Unfinished(NoPermissionMessage, UnfinishedInfo.NotifyType.Warning);

            var okR = await _store.SetSortOrderAsync(pagePk, sortOrder, Stamp());
            if (!okR.Ok) okR.Throw();
            if (!okR.Value)
                return Returning.Unfinished("Página no encontrada", UnfinishedInfo.NotifyType.Warning);
            return Returning.Success();
        }, saveLog: true);

    public Task<Returning> SetPageIconAsync(Guid pagePk, string? icon, string? iconColor) =>
        Returning.TryTask(async () =>
        {
            if (!_user.CanEdit())
                return Returning.Unfinished(NoPermissionMessage, UnfinishedInfo.NotifyType.Warning);

            var okR = await _store.SetPageIconAsync(pagePk, Normalize(icon), Normalize(iconColor), Stamp());
            if (!okR.Ok) okR.Throw();
            if (!okR.Value)
                return Returning.Unfinished("Página no encontrada", UnfinishedInfo.NotifyType.Warning);
            return Returning.Success();

            static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }, saveLog: true);

    public Task<Returning> DeletePageAsync(Guid pagePk) =>
        Returning.TryTask(async () =>
        {
            if (!_user.CanEdit())
                return Returning.Unfinished(NoPermissionMessage, UnfinishedInfo.NotifyType.Warning);

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

            var countR = await _store.SoftDeletePagesAsync(toDelete, Stamp());
            if (!countR.Ok) countR.Throw();
            if (countR.Value == 0)
                return Returning.Unfinished("Página no encontrada", UnfinishedInfo.NotifyType.Warning);
            return Returning.Success();
        }, saveLog: true);

    #endregion

    #region Permissions & search

    public Task<Returning<PagePermissionsDto>> GetPermissionsAsync(Guid pagePk) =>
        Returning<PagePermissionsDto>.TryTask(async () =>
        {
            if (!_user.CanManagePermissions(_options))
                return Returning.Unfinished(NoPermissionMessage, UnfinishedInfo.NotifyType.Warning);

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

    /// <summary>Uploads every pasted data-URI image and rewrites its src to a stable docimg:// reference.</summary>
    private async Task<string> InterceptDataUrisAsync(string html)
    {
        var matches = KnowledgeHubHtml.DataUriRegex().Matches(html);
        if (matches.Count == 0) return html;

        var sb = new StringBuilder(html);
        // Replace from last to first so earlier match indices stay valid.
        foreach (var match in matches.OrderByDescending(m => m.Index))
        {
            byte[] bytes;
            try { bytes = Convert.FromBase64String(match.Groups["data"].Value); }
            catch { continue; }

            var uploaded = await _imageService.UploadOrReplaceAsync(bytes, "pasted.webp");
            if (!uploaded.OkNotNull) continue;

            sb.Remove(match.Index, match.Length);
            sb.Insert(match.Index, KnowledgeHubHtml.DocImgUrl(uploaded.Value));
        }
        return sb.ToString();
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
