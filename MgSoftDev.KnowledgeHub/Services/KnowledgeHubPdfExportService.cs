using MgSoftDev.KnowledgeHub.Contracts;
using MgSoftDev.KnowledgeHub.Dtos;
using MgSoftDev.KnowledgeHub.Security;
using MgSoftDev.ReturningCore;
using MgSoftDev.ReturningCore.Helper;

namespace MgSoftDev.KnowledgeHub.Services;

/// <summary>
/// Collects published pages into an export document and hands it to whatever renderer is
/// registered. Every security decision happens here, never in the renderer.
/// </summary>
public sealed class KnowledgeHubPdfExportService : IKnowledgeHubPdfExportService
{
    private const string NoPermissionMessage = "No tienes permiso para realizar esta acción";

    private readonly IKnowledgeHubPageService _pages;
    private readonly IKnowledgeHubStore _store;
    private readonly IKnowledgeHubUserContext _user;
    private readonly KnowledgeHubOptions _options;
    private readonly IKnowledgeHubPdfRenderer? _renderer;

    // The renderer is OPTIONAL, like the sanitizer: without the MgSoftDev.KnowledgeHub.Pdf package
    // (or a host implementation) BuildAsync still works and ExportAsync reports it politely.
    public KnowledgeHubPdfExportService(IKnowledgeHubPageService pages, IKnowledgeHubStore store,
        IKnowledgeHubUserContext user, KnowledgeHubOptions options,
        IKnowledgeHubPdfRenderer? renderer = null)
    {
        _pages = pages;
        _store = store;
        _user = user;
        _options = options;
        _renderer = renderer;
    }

    public Task<Returning<PdfExportDocument>> BuildAsync(Guid rootPagePk, bool includeDescendants) =>
        Returning<PdfExportDocument>.TryTask(async () =>
        {
            if (!_user.CanExport(_options))
                return Returning.Unfinished(NoPermissionMessage, UnfinishedInfo.NotifyType.Warning);

            // The tree is already filtered by visibility AND by ancestor inheritance, so a page
            // whose parent the user cannot see is not in here either.
            var treeR = await _pages.GetTreeAsync();
            if (!treeR.Ok) treeR.Throw();

            var root = FindNode(treeR.Value!, rootPagePk);
            if (root is null)
                return Returning.Unfinished("La página no existe o no tienes permiso para verla",
                    UnfinishedInfo.NotifyType.Warning);

            // Asked for this page alone, and this page is the one marked: say so. Letting it fall
            // through to the count below would blame it on not being published, which is a lie and
            // sends the user to look in the wrong place.
            if (!includeDescendants && root.ExcludeFromPdf)
                return Returning.Unfinished(
                    "Esta página está marcada como no exportable a PDF. Quita la marca en Gestionar " +
                    "página, o exporta desde la página superior para incluir sus subpáginas.",
                    UnfinishedInfo.NotifyType.Warning);

            var planned = new List<(PageTreeNodeDto Node, int Level)>();
            Collect(root, 1, includeDescendants, planned);

            if (planned.Count == 0)
                return Returning.Unfinished(
                    includeDescendants
                        ? "Ni esta página ni sus subpáginas están publicadas todavía, o están marcadas " +
                          "como no exportables"
                        : "Esta página aún no ha sido publicada",
                    UnfinishedInfo.NotifyType.Warning);

            // Refuse rather than truncate: half a manual that looks whole is worse than an error.
            if (planned.Count > _options.MaxExportPages)
                return Returning.Unfinished(
                    $"La exportación tendría {planned.Count} páginas y el límite es {_options.MaxExportPages}. " +
                    "Exporta una rama más pequeña o sube MaxExportPages.",
                    UnfinishedInfo.NotifyType.Warning);

            var document = new PdfExportDocument
            {
                Title = root.Title,
                GeneratedAt = DateTime.Now,
                GeneratedBy = string.IsNullOrWhiteSpace(_user.DisplayName) ? null : _user.DisplayName
            };

            foreach (var (node, level) in planned)
            {
                // Defence in depth: the tree said this page is visible and published, but the read
                // path is the authority. A page that is rejected here is SKIPPED, not fatal — the
                // tree may have gone stale between the two calls.
                var readR = await _pages.GetPageForReadAsync(node.Pk);
                if (!readR.OkNotNull) continue;

                var page = readR.Value;

                // A page that was created and published but never written would print as a heading
                // followed by a blank sheet. This has to happen HERE and not while planning, because
                // the content is only known once the page is read — the price is that an empty page
                // still counts towards MaxExportPages, which is not worth a second trip to the store.
                if (KnowledgeHubHtml.IsVisuallyEmpty(page.ContentHtml)) continue;

                document.Sections.Add(new PdfExportSection
                {
                    PagePk = node.Pk,
                    Title = page.Title,
                    Level = level,
                    ContentHtml = page.ContentHtml,
                    VersionNumber = page.VersionNumber,
                    PublishedAt = page.PublishedAt
                });
            }

            if (document.Sections.Count == 0)
                return Returning.Unfinished(
                    "No hay contenido que exportar: las páginas están vacías o dejaron de ser visibles",
                    UnfinishedInfo.NotifyType.Warning);

            await AttachImagesAsync(document);
            return document;
        }, saveLog: true);

    public Task<Returning<PdfFileDto>> ExportAsync(Guid rootPagePk, bool includeDescendants) =>
        Returning<PdfFileDto>.TryTask(async () =>
        {
            // Capability first: answering "no renderer registered" to someone who may not export
            // leaks how the server is configured before they have earned an answer at all.
            if (!_user.CanExport(_options))
                return Returning.Unfinished(NoPermissionMessage, UnfinishedInfo.NotifyType.Warning);

            if (_renderer is null)
                return Returning.Unfinished(
                    "No hay ningún generador de PDF registrado. Añade el paquete " +
                    "MgSoftDev.KnowledgeHub.Pdf y llama a AddKnowledgeHubPdf().",
                    UnfinishedInfo.NotifyType.Warning);

            var buildR = await BuildAsync(rootPagePk, includeDescendants);
            if (!buildR.Ok)
            {
                if (buildR.UnfinishedInfo is { } reason) return Repeat(reason);
                buildR.Throw();
            }

            var document = buildR.Value!;
            var bytesR = await _renderer.RenderAsync(document);
            if (!bytesR.Ok)
            {
                if (bytesR.UnfinishedInfo is { } reason) return Repeat(reason);
                bytesR.Throw();
            }

            return new PdfFileDto
            {
                FileName = BuildFileName(document.Title),
                ContentType = "application/pdf",
                Content = bytesR.Value!
            };
        }, saveLog: true);

    /// <summary>
    /// Loads every image referenced across all the sections in a SINGLE store round-trip. Doing it
    /// per section would be one query per page, which for a branch is exactly the kind of N+1 the
    /// rest of the library avoids.
    /// </summary>
    private async Task AttachImagesAsync(PdfExportDocument document)
    {
        var pks = document.Sections
            .SelectMany(s => KnowledgeHubHtml.ExtractDocImagePks(s.ContentHtml))
            .Distinct()
            .ToList();

        if (pks.Count == 0) return;

        var blobsR = await _store.GetImageContentsAsync(pks);
        if (!blobsR.Ok) blobsR.Throw();

        foreach (var blob in blobsR.Value!)
        {
            // Width and Height stay at 0 on purpose: the store's blob carries no dimensions, and a
            // guessed size is worse than none — the renderer reads them off the bytes it decodes.
            document.Images[blob.ImagePk] = new PdfExportImage
            {
                Pk = blob.ImagePk,
                ContentType = "image/webp",
                Content = blob.Content
            };
        }
    }

    /// <summary>
    /// Re-raises a business rejection coming from a differently-typed Returning, keeping its title,
    /// message and notification type so the user sees the real reason and not a generic failure.
    /// </summary>
    private static ReturningError Repeat(UnfinishedInfo reason) =>
        reason.Mensaje is null
            ? Returning.Unfinished(reason.Title, reason.Type, reason.ErrorCode)
            : Returning.Unfinished(reason.Title, reason.Mensaje, reason.Type, reason.ErrorCode);

    private static PageTreeNodeDto? FindNode(IEnumerable<PageTreeNodeDto> nodes, Guid pk)
    {
        foreach (var node in nodes)
        {
            if (node.Pk == pk) return node;
            if (FindNode(node.Children, pk) is { } found) return found;
        }
        return null;
    }

    /// <summary>
    /// Depth-first in tree order, keeping only what is actually published and not marked out of
    /// exports. Skipping a page does NOT skip its children — a board of links is excluded, the
    /// manual hanging from it is not. Same shape as the published check right next to it.
    /// <para>
    /// The filtering happens HERE, while planning, and not in the read loop: the page limit is
    /// measured against this list, so a page filtered later would still eat quota from a branch
    /// its author deliberately left out.
    /// </para>
    /// </summary>
    private static void Collect(PageTreeNodeDto node, int level, bool includeDescendants,
        List<(PageTreeNodeDto, int)> into)
    {
        if (node.HasPublishedVersion && !node.ExcludeFromPdf) into.Add((node, level));
        if (!includeDescendants) return;

        foreach (var child in node.Children)
            Collect(child, level + 1, true, into);
    }

    private static string BuildFileName(string title)
    {
        var slug = KnowledgeHubSlug.Slugify(title);
        if (string.IsNullOrWhiteSpace(slug)) slug = "documento";
        return $"{slug}.pdf";
    }
}
