using MgSoftDev.KnowledgeHub;
using MgSoftDev.KnowledgeHub.Contracts;
using MgSoftDev.KnowledgeHub.Dtos;
using MgSoftDev.KnowledgeHub.Security;
using MgSoftDev.KnowledgeHub.Seeding;
using MgSoftDev.ReturningCore;
using MgSoftDev.ReturningCore.Exceptions;
using MgSoftDev.ReturningCore.Helper;
using Microsoft.Extensions.DependencyInjection;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace KnowledgeHub.ParityHarness;

/// <summary>
/// The single parity script every storage provider must pass with IDENTICAL output:
/// InMemory (F2), LiteDB (F3), SQL Server (F4) and the HTTP client/server pair (F8).
/// </summary>
public static class ParityScript
{
    private static int _passed;
    private static int _failed;

    /// <summary>
    /// Runs the full parity script against the services in <paramref name="sp"/>. In HTTP mode
    /// the client provider has no store, so <paramref name="seederProvider"/> points at the
    /// SERVER's provider for the content seeder (defaults to <paramref name="sp"/>).
    /// </summary>
    public static async Task<int> RunAsync(IServiceProvider sp, HarnessUserContext user,
        IServiceProvider? seederProvider = null)
    {
        _passed = 0;
        _failed = 0;

        var pages = sp.GetRequiredService<IKnowledgeHubPageService>();
        var images = sp.GetRequiredService<IKnowledgeHubImageService>();
        var rewriter = sp.GetRequiredService<IKnowledgeHubHtmlImageRewriter>();
        var seeder = (seederProvider ?? sp).GetRequiredService<KnowledgeHubContentSeeder>();

        // ---- 1. Seed --------------------------------------------------------------
        user.SetUser("admin", "Administrador", KnowledgeHubPermissions.Admin);
        var seed1 = await seeder.SeedSampleContentIfEmptyAsync(new Dictionary<string, string[]>
        {
            ["documentacion-tecnica"] = new[] { "Docs.Tech" },
            ["produccion"] = new[] { "Docs.Prod" },
            ["oficinas"] = new[] { "Docs.Ofi" }
        });
        Check("Seed inicial ejecuta", seed1.Ok && seed1.Value);
        var seed2 = await seeder.SeedSampleContentIfEmptyAsync();
        Check("Seed es idempotente", seed2.Ok && !seed2.Value);

        // ---- 2. Árbol admin --------------------------------------------------------
        var adminTree = await pages.GetTreeAsync();
        Check("Árbol admin: 4 raíces", adminTree.Ok && adminTree.Value!.Count == 4);
        var manual = FindBySlug(adminTree.Value!, "manual-usuario")!;
        var tecnica = FindBySlug(adminTree.Value!, "documentacion-tecnica")!;
        var produccion = FindBySlug(adminTree.Value!, "produccion")!;
        var pasos = FindBySlug(adminTree.Value!, "primeros-pasos")!;
        Check("Árbol admin: manual tiene 1 hijo", manual.Children.Count == 1);

        // ---- 3. Árbol usuario restringido -------------------------------------------
        user.SetUser("prod1", "Producción", "Docs.Prod");
        var prodTree = await pages.GetTreeAsync();
        Check("Árbol prod1: 2 raíces (públicas + su permiso)", prodTree.Ok && prodTree.Value!.Count == 2);
        Check("Árbol prod1: no ve 'documentacion-tecnica'", FindBySlug(prodTree.Value!, "documentacion-tecnica") is null);

        // ---- 4. Herencia de visibilidad ---------------------------------------------
        user.SetUser("admin", "Administrador", KnowledgeHubPermissions.Admin);
        var childR = await pages.CreatePageAsync(tecnica.Pk, "Hijo Público", "hijo-publico");
        Check("Crear hijo bajo página restringida", childR.Ok);
        var setPubR = await pages.SetPermissionsAsync(childR.Value, true, Array.Empty<string>());
        Check("Marcar hijo como público", setPubR.Ok);
        user.SetUser("prod1", "Producción", "Docs.Prod");
        var prodTree2 = await pages.GetTreeAsync();
        Check("Herencia: hijo público oculto porque el padre no es visible",
            FindBySlug(prodTree2.Value!, "hijo-publico") is null);

        // ---- 5. Versionado MAX+1 -----------------------------------------------------
        user.SetUser("editor1", "Editor", KnowledgeHubPermissions.Edit);
        var edit1 = await pages.GetPageForEditAsync(manual.Pk);
        Check("Editar: BaseVersionNumber = 1", edit1.OkNotNull && edit1.Value!.BaseVersionNumber == 1);
        edit1.Value!.ContentHtml += "<p>Cambio A</p>";
        var save1 = await pages.SaveDraftAsync(edit1.Value);
        Check("Guardar borrador → versión 2", save1.Ok && save1.Value == 2);
        edit1.Value.ContentHtml += "<p>Cambio B</p>";
        edit1.Value.ChangeNote = "Segundo cambio";
        var save2 = await pages.SaveDraftAsync(edit1.Value);
        Check("Guardar borrador → versión 3", save2.Ok && save2.Value == 3);

        // ---- 6. Publicación -----------------------------------------------------------
        var pub1 = await pages.PublishAsync(manual.Pk, 3);
        Check("Publicar con base actual", pub1.Ok);
        var read1 = await pages.GetPageForReadAsync(manual.Pk);
        Check("Lector ve la versión 3", read1.OkNotNull && read1.Value!.VersionNumber == 3);

        // ---- 7. Conflicto de publicación ----------------------------------------------
        var pub2 = await pages.PublishAsync(manual.Pk, 1);
        Check("Conflicto detectado (Unfinished Warning)", IsUnfinishedContaining(pub2, "más reciente"));

        // ---- 8. Historial y restaurar ---------------------------------------------------
        var versions1 = await pages.GetVersionsAsync(manual.Pk);
        Check("Historial: 3 versiones", versions1.Ok && versions1.Value!.Count == 3);
        Check("Historial: v3 marcada como publicada",
            versions1.Ok && versions1.Value!.First(v => v.VersionNumber == 3).IsCurrentPublished);
        Check("Historial: autor registrado",
            versions1.Ok && versions1.Value!.First(v => v.VersionNumber == 3).AuthorName == "editor1");
        var v1Pk = versions1.Value!.First(v => v.VersionNumber == 1).Pk;
        var restore = await pages.RestoreVersionAsync(v1Pk);
        Check("Restaurar v1", restore.Ok);
        var versions2 = await pages.GetVersionsAsync(manual.Pk);
        Check("Restaurar crea la versión 4 (no borra historial)",
            versions2.Ok && versions2.Value!.Count == 4 &&
            versions2.Value![0].VersionNumber == 4 &&
            versions2.Value![0].ChangeNote == "Restaurado desde la versión 1");

        // ---- 9. Búsqueda (case-insensitive + permisos) ----------------------------------
        user.SetUser("prod1", "Producción", "Docs.Prod");
        var search1 = await pages.SearchAsync("PLANTA");
        Check("Búsqueda case-insensitive encuentra 'Producción'",
            search1.Ok && search1.Value!.Any(r => r.Slug == "produccion"));
        user.SetUser("ofi1", "Oficinas", "Docs.Ofi");
        var search2 = await pages.SearchAsync("PLANTA");
        Check("Búsqueda respeta permisos (ofi1 no ve producción)",
            search2.Ok && search2.Value!.All(r => r.Slug != "produccion"));

        // ---- 10. Deduplicación de imágenes -----------------------------------------------
        user.SetUser("editor1", "Editor", KnowledgeHubPermissions.Edit);
        var pngBytes = MakeTestPng(200, 100);
        var up1 = await images.UploadOrReplaceAsync(pngBytes, "test.png");
        var up2 = await images.UploadOrReplaceAsync(pngBytes, "test-otra-vez.png");
        Check("Subida de imagen", up1.Ok);
        Check("Dedup: misma imagen → mismo Pk", up2.Ok && up1.Value == up2.Value);

        // ---- 11. Intercepción de data-URIs ------------------------------------------------
        var editPasos = await pages.GetPageForEditAsync(pasos.Pk);
        editPasos.Value!.ContentHtml += $"<p><img src=\"data:image/png;base64,{Convert.ToBase64String(pngBytes)}\"></p>";
        var savePasos = await pages.SaveDraftAsync(editPasos.Value);
        Check("Guardar con data-URI", savePasos.Ok);
        var editPasos2 = await pages.GetPageForEditAsync(pasos.Pk);
        Check("Data-URI interceptada → docimg://",
            editPasos2.OkNotNull &&
            editPasos2.Value!.ContentHtml.Contains("docimg://") &&
            !editPasos2.Value!.ContentHtml.Contains("data:image"));

        // ---- 12. Rewriter + caché por hash --------------------------------------------------
        var read2 = await pages.GetPageForReadAsync(manual.Pk);
        var rewrite1 = await rewriter.PrepareForDisplayAsync(read2.Value!.ContentHtml);
        Check("Rewriter genera URLs de display",
            rewrite1.OkNotNull && rewrite1.Value!.Html.Contains("/kh/assets/") && rewrite1.Value!.Html.Contains(".webp"));
        Check("Primera vez: cache miss", rewrite1.OkNotNull && rewrite1.Value!.CacheMisses >= 1);
        var rewrite2 = await rewriter.PrepareForDisplayAsync(read2.Value!.ContentHtml);
        Check("Segunda vez: cache hit",
            rewrite2.OkNotNull && rewrite2.Value!.CacheMisses == 0 && rewrite2.Value!.CacheHits >= 1);

        // ---- 13. Reversión de URLs de display al guardar -------------------------------------
        var editManual = await pages.GetPageForEditAsync(manual.Pk);
        var displayR = await rewriter.PrepareForDisplayAsync(editManual.Value!.ContentHtml);
        editManual.Value!.ContentHtml = displayR.Value!.Html;
        var saveManual = await pages.SaveDraftAsync(editManual.Value);
        Check("Guardar HTML con URLs de display", saveManual.Ok);
        var editManual2 = await pages.GetPageForEditAsync(manual.Pk);
        Check("URLs revertidas a docimg:// sin duplicar",
            editManual2.OkNotNull &&
            editManual2.Value!.ContentHtml.Contains("docimg://") &&
            !editManual2.Value!.ContentHtml.Contains("/kh/assets/"));

        // ---- 14. Permisos replace-all ----------------------------------------------------------
        user.SetUser("admin", "Administrador", KnowledgeHubPermissions.Admin);
        var setPerm = await pages.SetPermissionsAsync(produccion.Pk, false, new[] { "Docs.Prod2", "docs.prod2", "Docs.X" });
        Check("SetPermissions", setPerm.Ok);
        var getPerm = await pages.GetPermissionsAsync(produccion.Pk);
        Check("Replace-all + dedup case-insensitive",
            getPerm.OkNotNull && getPerm.Value!.Permissions.Count == 2 && !getPerm.Value!.IsPublic);

        // ---- 15. Guards de autorización server-side ----------------------------------------------
        user.SetUser("viewer", "Solo Lectura");
        var denied1 = await pages.SaveDraftAsync(new PageEditDto { PagePk = manual.Pk, Title = "x", Slug = "x" });
        Check("Sin permiso Edit no puede guardar", IsUnfinishedContaining(denied1, "permiso"));
        var denied2 = await pages.CreatePageAsync(null, "X", "x-slug");
        Check("Sin permiso Edit no puede crear", IsUnfinishedContaining(denied2, "permiso"));

        // ---- 16. Slug duplicado -------------------------------------------------------------------
        user.SetUser("editor1", "Editor", KnowledgeHubPermissions.Edit);
        var dupSlug = await pages.CreatePageAsync(null, "Otra", "manual-usuario");
        Check("Slug duplicado rechazado", IsUnfinishedContaining(dupSlug, "slug"));

        // ---- 17. Mover: detección de ciclos ----------------------------------------------------------
        var pageA = await pages.CreatePageAsync(null, "Página A", "pagina-a");
        var pageB = await pages.CreatePageAsync(pageA.Value, "Página B", "pagina-b");
        var cycle = await pages.MovePageAsync(pageA.Value, pageB.Value);
        Check("Mover bajo descendiente rechazado", IsUnfinishedContaining(cycle, "descendientes"));

        // ---- 18. Borrado lógico de subárbol ------------------------------------------------------------
        var delete = await pages.DeletePageAsync(pageA.Value);
        Check("Borrar subárbol", delete.Ok);
        user.SetUser("admin", "Administrador", KnowledgeHubPermissions.Admin);
        var finalTree = await pages.GetTreeAsync();
        Check("Subárbol eliminado (A y B fuera del árbol)",
            finalTree.Ok &&
            FindBySlug(finalTree.Value!, "pagina-a") is null &&
            FindBySlug(finalTree.Value!, "pagina-b") is null);

        // ---- 19. Caminos "no encontrado" (Value null del store, sin excepciones) ---------------------------
        var missingPk = Guid.NewGuid();
        var readMissing = await pages.GetPageForReadAsync(missingPk);
        Check("Leer página inexistente → Unfinished", IsUnfinishedContaining(readMissing, "no existe"));
        var permsMissing = await pages.GetPermissionsAsync(missingPk);
        Check("Permisos de página inexistente → Unfinished", IsUnfinishedContaining(permsMissing, "no encontrada"));
        var editMissing = await pages.GetPageForEditAsync(missingPk);
        Check("Editar página inexistente → Unfinished", IsUnfinishedContaining(editMissing, "no encontrada"));
        var versionMissing = await pages.GetVersionContentAsync(missingPk);
        Check("Versión inexistente → Unfinished", IsUnfinishedContaining(versionMissing, "no encontrada"));

        // Página nueva: sin versiones (GetLatestVersion null) y sin publicar (lector avisa).
        var fresh = await pages.CreatePageAsync(null, "Página Fresca", "pagina-fresca");
        Check("Crear página fresca", fresh.Ok);
        var editFresh = await pages.GetPageForEditAsync(fresh.Value);
        Check("Editar página sin versiones → BaseVersionNumber 0",
            editFresh.OkNotNull && editFresh.Value!.BaseVersionNumber == 0 && editFresh.Value!.Title == "Página Fresca");
        var readFresh = await pages.GetPageForReadAsync(fresh.Value);
        Check("Leer página sin publicar → Unfinished", IsUnfinishedContaining(readFresh, "no ha sido publicada"));

        Console.WriteLine();
        Console.WriteLine($"===== RESULTADO: {_passed} PASS / {_failed} FAIL =====");
        return _failed;
    }

    // ---------------------------------------------------------------- Helpers

    private static void Check(string name, bool condition)
    {
        if (condition)
        {
            _passed++;
            Console.WriteLine($"  [PASS] {name}");
        }
        else
        {
            _failed++;
            Console.WriteLine($"  [FAIL] {name}");
        }
    }

    private static bool IsUnfinishedContaining(ReturningBase result, string text) =>
        !result.Ok && result.UnfinishedInfo is { } unfinished &&
        $"{unfinished.Title} {unfinished.Mensaje}".Contains(text, StringComparison.OrdinalIgnoreCase);

    private static PageTreeNodeDto? FindBySlug(IEnumerable<PageTreeNodeDto> nodes, string slug)
    {
        foreach (var node in nodes)
        {
            if (node.Slug == slug) return node;
            if (FindBySlug(node.Children, slug) is { } found) return found;
        }
        return null;
    }

    private static byte[] MakeTestPng(int width, int height)
    {
        using var image = new Image<Rgba32>(width, height, new Rgba32(45, 90, 180, 255));
        using var ms = new MemoryStream();
        image.SaveAsPng(ms);
        return ms.ToArray();
    }
}
