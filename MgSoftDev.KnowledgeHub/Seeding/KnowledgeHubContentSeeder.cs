using System.Security.Cryptography;
using MgSoftDev.KnowledgeHub.Contracts;
using MgSoftDev.KnowledgeHub.Entities;
using MgSoftDev.KnowledgeHub.Enums;
using MgSoftDev.KnowledgeHub.Services;
using MgSoftDev.KnowledgeHub.Store;
using MgSoftDev.ReturningCore;
using MgSoftDev.ReturningCore.Exceptions;
using MgSoftDev.ReturningCore.Helper;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.PixelFormats;

namespace MgSoftDev.KnowledgeHub.Seeding;

/// <summary>
/// Optional demo-content seeding: a small published tree with procedurally generated WebP
/// images (no external files), stored through the same hashing/dedup path the editor uses.
/// Users/roles are the HOST's responsibility — this seeder only creates pages and images.
/// </summary>
public sealed class KnowledgeHubContentSeeder
{
    private const string SeedUserName = "seed";

    private readonly IKnowledgeHubStore _store;

    public KnowledgeHubContentSeeder(IKnowledgeHubStore store)
    {
        _store = store;
    }

    /// <summary>
    /// Seeds the sample tree only when there are no active pages. Returns true when it seeded.
    /// <paramref name="permissionsBySlug"/> maps the restricted demo slugs
    /// ("documentacion-tecnica", "produccion", "oficinas") to host permission names; slugs
    /// without an entry stay non-public with no permissions (only admins see them).
    /// </summary>
    public Task<Returning<bool>> SeedSampleContentIfEmptyAsync(
        IReadOnlyDictionary<string, string[]>? permissionsBySlug = null) =>
        Returning<bool>.TryTask(async () =>
        {
            var linksR = await _store.GetActivePageLinksAsync();
            if (!linksR.Ok) linksR.Throw();
            if (linksR.Value!.Count > 0) return false;

            var now = DateTime.Now;
            var audit = new AuditStamp(SeedUserName, now);

            // ---- Pages -------------------------------------------------------------
            var manual = await InsertPageAsync(null, "manual-usuario", "Manual de Usuario", isPublic: true, sortOrder: 1, now, icon: "menu_book", iconColor: "#2563eb");
            var pasos = await InsertPageAsync(manual.Pk, "primeros-pasos", "Primeros Pasos", isPublic: true, sortOrder: 1, now, icon: "task", iconColor: "#059669");
            var tecnica = await InsertPageAsync(null, "documentacion-tecnica", "Documentación Técnica", isPublic: false, sortOrder: 2, now, icon: "code", iconColor: "#7c3aed");
            var produccion = await InsertPageAsync(null, "produccion", "Producción", isPublic: false, sortOrder: 3, now, icon: "build", iconColor: "#ea580c");
            var oficinas = await InsertPageAsync(null, "oficinas", "Oficinas", isPublic: false, sortOrder: 4, now, icon: "badge");

            // ---- Demo images (same dedup path as real uploads) ----------------------
            var imgManual = await InsertGradientImageAsync("demo-manual.webp", (33, 150, 243), now);
            var imgTecnica = await InsertGradientImageAsync("demo-tecnica.webp", (76, 175, 80), now);
            var imgProduccion = await InsertGradientImageAsync("demo-produccion.webp", (244, 67, 54), now);

            // ---- Published content ---------------------------------------------------
            await SeedPublishedVersionAsync(manual,
                "<h1>Manual de Usuario</h1><p>Bienvenido al portal de documentación.</p>" +
                DemoImageHtml(imgManual), new[] { imgManual }, now, audit);
            await SeedPublishedVersionAsync(pasos,
                "<h1>Primeros Pasos</h1><ol><li>Inicia sesión.</li><li>Explora el árbol.</li></ol>",
                Array.Empty<Guid>(), now, audit);
            await SeedPublishedVersionAsync(tecnica,
                "<h1>Documentación Técnica</h1><p>Referencia para el equipo de desarrollo.</p>" +
                DemoImageHtml(imgTecnica), new[] { imgTecnica }, now, audit);
            await SeedPublishedVersionAsync(produccion,
                "<h1>Producción</h1><p>Procesos de la planta.</p>" +
                DemoImageHtml(imgProduccion), new[] { imgProduccion }, now, audit);
            await SeedPublishedVersionAsync(oficinas,
                "<h1>Oficinas</h1><p>Documentación administrativa.</p>",
                Array.Empty<Guid>(), now, audit);

            // ---- Visibility of the restricted pages ---------------------------------
            if (permissionsBySlug is not null)
            {
                var bySlug = new Dictionary<string, DocPage>(StringComparer.OrdinalIgnoreCase)
                {
                    [tecnica.Slug] = tecnica,
                    [produccion.Slug] = produccion,
                    [oficinas.Slug] = oficinas
                };

                foreach (var (slug, permissions) in permissionsBySlug)
                {
                    if (!bySlug.TryGetValue(slug, out var page)) continue;
                    var setR = await _store.SetPagePermissionsAsync(page.Pk, isPublic: false, permissions, audit);
                    if (!setR.Ok) setR.Throw();
                }
            }

            return true;
        }, saveLog: true);

    private async Task<DocPage> InsertPageAsync(Guid? parentPk, string slug, string title, bool isPublic, int sortOrder, DateTime now,
        string? icon = null, string? iconColor = null)
    {
        var page = new DocPage
        {
            Fk_DocPageParent = parentPk,
            Slug = slug,
            Title = title,
            SortOrder = sortOrder,
            IsPublic = isPublic,
            Icon = icon,
            IconColor = iconColor
        };
        EntityStamp.PrepareNew(page, SeedUserName, now);

        var insertR = await _store.InsertPageAsync(page);
        if (!insertR.Ok) insertR.Throw();
        return page;
    }

    private async Task SeedPublishedVersionAsync(DocPage page, string html, IReadOnlyList<Guid> imagePks,
        DateTime now, AuditStamp audit)
    {
        var version = new DocPageVersion
        {
            Fk_DocPage = page.Pk,
            VersionNumber = 1,
            Title = page.Title,
            ContentHtml = html,
            Status = DocPageStatus.Draft,
            ChangeNote = "Versión inicial"
        };
        EntityStamp.PrepareNew(version, SeedUserName, now);

        var insertR = await _store.InsertVersionAsync(version, imagePks);
        if (!insertR.Ok) insertR.Throw();

        // Publish through the store so the pointer/status flow matches production behavior.
        var publishR = await _store.TryPublishAsync(page.Pk, baseVersionNumber: 0, audit);
        if (!publishR.Ok) publishR.Throw();
    }

    private async Task<Guid> InsertGradientImageAsync(string fileName, (byte R, byte G, byte B) baseColor, DateTime now)
    {
        var (webp, width, height) = GenerateGradientWebp(640, 400, baseColor);
        var hash = Convert.ToHexString(SHA256.HashData(webp)).ToLowerInvariant();

        var existingR = await _store.GetImageRefsByHashesAsync(new[] { hash });
        if (!existingR.Ok) existingR.Throw();
        if (existingR.Value!.FirstOrDefault() is { } existing)
            return existing.Pk;

        var image = new DocImage
        {
            FileName = fileName,
            ContentHash = hash,
            ContentType = "image/webp",
            SizeBytes = webp.LongLength,
            Width = width,
            Height = height
        };
        EntityStamp.PrepareNew(image, SeedUserName, now);

        var insertR = await _store.InsertImageAsync(image, webp);
        if (!insertR.Ok) insertR.Throw();
        return image.Pk;
    }

    private static string DemoImageHtml(Guid imagePk) =>
        $"<p><img src=\"{KnowledgeHubHtml.DocImgUrl(imagePk)}\" alt=\"Imagen de ejemplo\" /></p>";

    /// <summary>Builds a small two-axis gradient image and encodes it as WebP.</summary>
    private static (byte[] Bytes, int Width, int Height) GenerateGradientWebp(int width, int height, (byte R, byte G, byte B) baseColor)
    {
        using var image = new Image<Rgba32>(width, height);
        image.ProcessPixelRows(accessor =>
        {
            for (var y = 0; y < accessor.Height; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (var x = 0; x < row.Length; x++)
                {
                    row[x] = new Rgba32(
                        (byte)((baseColor.R + x * 120 / width) % 256),
                        (byte)((baseColor.G + y * 120 / height) % 256),
                        baseColor.B,
                        255);
                }
            }
        });

        using var stream = new MemoryStream();
        image.SaveAsWebp(stream, new WebpEncoder());
        return (stream.ToArray(), width, height);
    }
}
