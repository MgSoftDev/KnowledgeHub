using MgSoftDev.KnowledgeHub;
using MgSoftDev.KnowledgeHub.Contracts;
using MgSoftDev.KnowledgeHub.Dtos;
using MgSoftDev.KnowledgeHub.Enums;
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

        // Formato no decodificable (SVG): debe RECHAZAR el guardado. Si se aceptara, el blob
        // base64 quedaría inline en ContentHtml y se duplicaría en cada versión posterior.
        var versionBeforeSvg = editPasos2.Value!.BaseVersionNumber;
        var svgBytes = System.Text.Encoding.UTF8.GetBytes(
            "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"10\" height=\"10\"><rect width=\"10\" height=\"10\"/></svg>");
        editPasos2.Value!.ContentHtml += $"<p><img src=\"data:image/svg+xml;base64,{Convert.ToBase64String(svgBytes)}\"></p>";
        var saveSvg = await pages.SaveDraftAsync(editPasos2.Value);
        Check("Data-URI no soportada (SVG) rechaza el guardado", IsUnfinishedContaining(saveSvg, "image/svg+xml"));

        var editPasos3 = await pages.GetPageForEditAsync(pasos.Pk);
        Check("SVG rechazado: no se creó versión nueva",
            editPasos3.OkNotNull && editPasos3.Value!.BaseVersionNumber == versionBeforeSvg);
        Check("SVG rechazado: no quedó base64 en el HTML almacenado",
            editPasos3.OkNotNull && !editPasos3.Value!.ContentHtml.Contains(";base64,"));

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

        // ---- 20. Icono + color por página --------------------------------------------------------------
        var manualNode = FindBySlug(finalTree.Value!, "manual-usuario");
        Check("Seed: icono llega al nodo del árbol (menu_book)",
            manualNode is { Icon: "menu_book", IconColor: "#2563eb" });
        var manualPk = manualNode!.Pk;

        var setIcon = await pages.SetPageIconAsync(manualPk, "rocket_launch", "#db2777");
        Check("SetPageIcon OK", setIcon.Ok);

        var infoIcon = await pages.GetPageInfoAsync(manualPk);
        Check("Icono se lee en GetPageInfo",
            infoIcon.OkNotNull && infoIcon.Value!.Icon == "rocket_launch" && infoIcon.Value!.IconColor == "#db2777");

        var treeIcon = await pages.GetTreeAsync();
        Check("Icono se lee en el nodo del árbol",
            treeIcon.Ok && FindBySlug(treeIcon.Value!, "manual-usuario") is { Icon: "rocket_launch", IconColor: "#db2777" });

        var editIcon = await pages.GetPageForEditAsync(manualPk);
        Check("Icono se lee en GetPageForEdit",
            editIcon.OkNotNull && editIcon.Value!.Icon == "rocket_launch" && editIcon.Value!.IconColor == "#db2777");

        var readIcon = await pages.GetPageForReadAsync(manualPk);
        Check("Icono se lee en GetPageForRead",
            readIcon.OkNotNull && readIcon.Value!.Icon == "rocket_launch" && readIcon.Value!.IconColor == "#db2777");

        var clearIcon = await pages.SetPageIconAsync(manualPk, null, null);
        var infoCleared = await pages.GetPageInfoAsync(manualPk);
        Check("Limpiar icono (null) funciona",
            clearIcon.Ok && infoCleared.OkNotNull && infoCleared.Value!.Icon is null && infoCleared.Value!.IconColor is null);

        // ---- 21. Saneado del HTML al guardar ------------------------------------------------------------
        // El sanitizador se registra en el contenedor del arnés; en modo http vive en el SERVIDOR,
        // así que esto también prueba que la limpieza ocurre server-side aunque el cliente no la haga.
        var editSan = await pages.GetPageForEditAsync(pasos.Pk);
        editSan.Value!.ContentHtml =
            "<h1>Título<o:p></o:p></h1>" +
            "<p class=\"MsoNormal\">Texto<o:p>&nbsp;</o:p></p>" +
            "<!--[if gte vml 1]><v:shape id=\"x\" style='width:159pt'>" +
            "<v:imagedata src=\"file:///C:/tmp/clip.png\"/></v:shape><![endif]-->" +
            "<p onclick=\"evil()\">click</p><script>alert(1)</script>";
        var saveSan = await pages.SaveDraftAsync(editSan.Value);
        Check("Guardar con basura de Word", saveSan.Ok);

        var editSan2 = await pages.GetPageForEditAsync(pasos.Pk);
        var cleanHtml = editSan2.OkNotNull ? editSan2.Value!.ContentHtml : string.Empty;
        Check("Saneado: se quitó la basura de Word y el script",
            editSan2.OkNotNull &&
            !cleanHtml.Contains("<o:p", StringComparison.OrdinalIgnoreCase) &&
            !cleanHtml.Contains("MsoNormal", StringComparison.OrdinalIgnoreCase) &&
            !cleanHtml.Contains("v:shape", StringComparison.OrdinalIgnoreCase) &&
            !cleanHtml.Contains("<script", StringComparison.OrdinalIgnoreCase) &&
            !cleanHtml.Contains("onclick", StringComparison.OrdinalIgnoreCase));
        Check("Saneado: conserva el contenido legítimo",
            cleanHtml.Contains("Título") && cleanHtml.Contains("Texto"));

        // Regresión crítica: si el esquema docimg:// o el CSS zoom no estuvieran permitidos, el
        // saneado destruiría las imágenes ya almacenadas y el tamaño que fija la tool de imagen.
        var editImg = await pages.GetPageForEditAsync(manualPk);
        var firstImg = System.Text.RegularExpressions.Regex.Match(
            editImg.Value!.ContentHtml, "<img[^>]*>", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        Check("La página tiene una imagen para la regresión", firstImg.Success);

        editImg.Value!.ContentHtml += "<p><img src=\"docimg://019f8d8a-f05a-7796-b5bf-ee2e48d67c29\" " +
                                      "alt=\"Q\" style=\"zoom:900%;width:100px;height:50px;\"></p>";
        var saveImg = await pages.SaveDraftAsync(editImg.Value);
        Check("Guardar con docimg:// y estilo de tamaño", saveImg.Ok);

        var editImg2 = await pages.GetPageForEditAsync(manualPk);
        var imgHtml = editImg2.OkNotNull ? editImg2.Value!.ContentHtml : string.Empty;
        Check("Saneado: conserva zoom/width/height en el style",
            imgHtml.Contains("zoom", StringComparison.OrdinalIgnoreCase) &&
            imgHtml.Contains("100px") && imgHtml.Contains("50px"));
        Check("Saneado: no destruye las imágenes existentes",
            System.Text.RegularExpressions.Regex.IsMatch(imgHtml, "<img[^>]+src=",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase));

        // ---- 22. Limpieza de imágenes huérfanas ---------------------------------------------------------
        user.SetUser("admin", "Administrador", KnowledgeHubPermissions.Admin);

        // Imagen A: se sube y se deja SIN usar en ninguna página → huérfana.
        var orphanBytes = MakeTestPng(30, 30);
        var orphanUp = await images.UploadOrReplaceAsync(orphanBytes, "huerfana.png");
        Check("Subir imagen que quedará huérfana", orphanUp.Ok);

        // Imagen B: se usa y DESPUÉS se quita del contenido, quedando solo en una versión ANTIGUA.
        // Es la regresión importante: la tabla de enlaces solo guarda la última versión, así que
        // basarse en ella la daría por huérfana y romperíamos el historial.
        var histBytes = MakeTestPng(40, 24);
        var histUp = await images.UploadOrReplaceAsync(histBytes, "solo-en-historial.png");
        var editHist = await pages.GetPageForEditAsync(produccion.Pk);
        editHist.Value!.ContentHtml = $"<p>Con imagen</p><p><img src=\"docimg://{histUp.Value}\"></p>";
        var saveHist1 = await pages.SaveDraftAsync(editHist.Value);
        var editHist2 = await pages.GetPageForEditAsync(produccion.Pk);
        editHist2.Value!.ContentHtml = "<p>Ya sin imagen</p>";
        var saveHist2 = await pages.SaveDraftAsync(editHist2.Value);
        Check("Imagen queda solo en una versión antigua", saveHist1.Ok && saveHist2.Ok);

        var analysis = await images.AnalyzeOrphanImagesAsync();
        Check("Analizar huérfanas devuelve reporte", analysis.OkNotNull);
        Check("La imagen sin usar se detecta como huérfana",
            analysis.OkNotNull && analysis.Value!.OrphanImages >= 1 &&
            analysis.Value!.OrphanBytes > 0);
        Check("Analizar no borra nada (total sin cambios)",
            analysis.OkNotNull &&
            analysis.Value!.TotalImages == analysis.Value!.OrphanImages + analysis.Value!.ReferencedImages);

        var deletedCount = await images.DeleteOrphanImagesAsync();
        Check("Eliminar huérfanas", deletedCount.Ok && deletedCount.Value >= 1);

        // La imagen que solo vive en el historial debe SEGUIR existiendo: se comprueba leyendo la
        // versión antigua y viendo que el rewriter aún resuelve su docimg:// a una URL de display
        // (si el binario se hubiera borrado, no habría hash que resolver y quedaría sin traducir).
        var histVersions = await pages.GetVersionsAsync(produccion.Pk);
        var oldVersionPk = histVersions.OkNotNull
            ? histVersions.Value!.OrderBy(v => v.VersionNumber)
                .LastOrDefault(v => v.VersionNumber == saveHist1.Value)?.Pk
            : null;
        var oldContent = oldVersionPk is { } vpk
            ? await pages.GetVersionContentAsync(vpk)
            : null;
        var oldRewritten = oldContent is { OkNotNull: true }
            ? await rewriter.PrepareForDisplayAsync(oldContent.Value!.ContentHtml)
            : null;
        Check("Imagen usada solo en el historial NO se borró",
            oldRewritten is { OkNotNull: true } &&
            oldRewritten.Value!.Html.Contains(".webp") &&
            !oldRewritten.Value!.Html.Contains("docimg://"));

        var afterDelete = await images.AnalyzeOrphanImagesAsync();
        Check("Tras limpiar no quedan huérfanas",
            afterDelete.OkNotNull && afterDelete.Value!.OrphanImages == 0);

        // Sin permiso de Admin no se puede hacer mantenimiento.
        user.SetUser("editor1", "Editor", KnowledgeHubPermissions.Edit);
        var deniedAnalyze = await images.AnalyzeOrphanImagesAsync();
        Check("Sin Admin no se puede analizar", IsUnfinishedContaining(deniedAnalyze, "administrador"));
        var deniedDelete = await images.DeleteOrphanImagesAsync();
        Check("Sin Admin no se puede eliminar", IsUnfinishedContaining(deniedDelete, "administrador"));
        user.SetUser("admin", "Administrador", KnowledgeHubPermissions.Admin);

        // ---- 23. Orden de hermanos: siempre 1..N ---------------------------------------------------------
        user.SetUser("admin", "Administrador", KnowledgeHubPermissions.Admin);

        var ordRoot = await pages.CreatePageAsync(null, "Orden Raíz", "orden-raiz");
        var ordA = await pages.CreatePageAsync(ordRoot.Value, "Orden A", "orden-a");
        var ordB = await pages.CreatePageAsync(ordRoot.Value, "Orden B", "orden-b");
        var ordC = await pages.CreatePageAsync(ordRoot.Value, "Orden C", "orden-c");
        var ordD = await pages.CreatePageAsync(ordRoot.Value, "Orden D", "orden-d");
        Check("Crear 4 hermanas", ordA.Ok && ordB.Ok && ordC.Ok && ordD.Ok);

        Check("Al crear, los hermanos quedan 1..N en orden de creación",
            await SiblingOrderAsync(pages, "orden-raiz") is ["Orden A", "Orden B", "Orden C", "Orden D"] &&
            await SiblingsAreContiguousAsync(pages, "orden-raiz"));

        // Subir C: A, C, B, D
        var upC = await pages.MovePageOrderAsync(ordC.Value, PageMoveDirection.Up);
        Check("Subir una página la intercambia con la anterior",
            upC.Ok && await SiblingOrderAsync(pages, "orden-raiz") is ["Orden A", "Orden C", "Orden B", "Orden D"]);

        // Bajar A: C, A, B, D
        var downA = await pages.MovePageOrderAsync(ordA.Value, PageMoveDirection.Down);
        Check("Bajar una página la intercambia con la siguiente",
            downA.Ok && await SiblingOrderAsync(pages, "orden-raiz") is ["Orden C", "Orden A", "Orden B", "Orden D"]);

        // Extremos: no-op y Ok (los botones de la UI van deshabilitados ahí).
        var upFirst = await pages.MovePageOrderAsync(ordC.Value, PageMoveDirection.Up);
        var downLast = await pages.MovePageOrderAsync(ordD.Value, PageMoveDirection.Down);
        Check("En los extremos subir/bajar es no-op y devuelve Ok",
            upFirst.Ok && downLast.Ok &&
            await SiblingOrderAsync(pages, "orden-raiz") is ["Orden C", "Orden A", "Orden B", "Orden D"]);

        // Borrar del medio → el grupo se cierra sin huecos.
        var delA = await pages.DeletePageAsync(ordA.Value);
        Check("Tras borrar, los hermanos quedan 1..N sin huecos",
            delA.Ok && await SiblingsAreContiguousAsync(pages, "orden-raiz") &&
            await SiblingOrderAsync(pages, "orden-raiz") is ["Orden C", "Orden B", "Orden D"]);

        // Mover a otro padre → llega la ÚLTIMA y el origen se cierra.
        var ordOther = await pages.CreatePageAsync(null, "Orden Otro", "orden-otro");
        var ordX = await pages.CreatePageAsync(ordOther.Value, "Orden X", "orden-x");
        var moveB = await pages.MovePageAsync(ordB.Value, ordOther.Value);
        Check("Al mover a otro padre queda el ÚLTIMO del destino",
            moveB.Ok && ordX.Ok &&
            await SiblingOrderAsync(pages, "orden-otro") is ["Orden X", "Orden B"]);
        Check("El grupo de origen queda 1..N tras mover",
            await SiblingsAreContiguousAsync(pages, "orden-raiz") &&
            await SiblingOrderAsync(pages, "orden-raiz") is ["Orden C", "Orden D"]);

        // Normalización global: ensuciar a propósito y comprobar que respeta el orden visible.
        await pages.ReorderAsync(ordC.Value, 40);
        await pages.ReorderAsync(ordD.Value, 90);
        var visibleBefore = await SiblingOrderAsync(pages, "orden-raiz");
        var normalized = await pages.NormalizeAllPageOrdersAsync();
        Check("Normalización global ejecuta", normalized.Ok);
        Check("Normalización respeta el orden visible y deja 1..N",
            await SiblingsAreContiguousAsync(pages, "orden-raiz") &&
            (await SiblingOrderAsync(pages, "orden-raiz")).SequenceEqual(visibleBefore));

        user.SetUser("editor1", "Editor", KnowledgeHubPermissions.Edit);
        var normDenied = await pages.NormalizeAllPageOrdersAsync();
        Check("Sin Admin no se puede normalizar", IsUnfinishedContaining(normDenied, "administrador"));
        user.SetUser("admin", "Administrador", KnowledgeHubPermissions.Admin);

        // ---- 24. Niveles de limpieza del HTML ------------------------------------------------------------
        // Se llama al sanitizador DIRECTAMENTE: los niveles solo los usan pegar y el botón manual,
        // que son de UI. Cada check fija una de las trampas verificadas al diseñarlo.
        var san = (seederProvider ?? sp).GetService<IKnowledgeHubHtmlSanitizer>();
        Check("Sanitizador disponible para los niveles", san is not null);

        if (san is not null)
        {
            string Clean(string html, HtmlCleanupLevel level) =>
                san.Sanitize(html, HtmlSanitizeContext.Paste, level);

            // Trampa 1: quitar el <span> NO debe llevarse el texto por delante.
            var spanOut = Clean("<h2>Titulo <span style=\"font-size:24px;color:#fff\">interno</span></h2>",
                HtmlCleanupLevel.Strict);
            Check("Nivel 2: desenvuelve el span SIN perder su texto",
                spanOut.Contains("interno") && !spanOut.Contains("<span", StringComparison.OrdinalIgnoreCase));

            // Trampa 4: el shorthand background se expande; no deben quedar longhands 'initial'.
            var darkOut = Clean(
                "<div style=\"background-color:#1e1e1e;color:#d4d4d4;font-family:Consolas;letter-spacing:.5px\">" +
                "<p style=\"white-space:pre\">const x = 1;</p></div>", HtmlCleanupLevel.Strict);
            Check("Nivel 2: quita colores/fuentes sin dejar residuos background-*",
                !darkOut.Contains("background", StringComparison.OrdinalIgnoreCase) &&
                !darkOut.Contains("color", StringComparison.OrdinalIgnoreCase) &&
                !darkOut.Contains("Consolas") && darkOut.Contains("const x = 1;"));

            // Lo que produce la propia librería debe sobrevivir al nivel 2.
            var imgOut = Clean("<img src=\"docimg://019f8d8a-f05a-7796-b5bf-ee2e48d67c29\" " +
                               "style=\"zoom:900%;width:100px;height:50px;\">", HtmlCleanupLevel.Strict);
            Check("Nivel 2: conserva el tamaño de las imágenes",
                imgOut.Contains("zoom") && imgOut.Contains("100px") && imgOut.Contains("50px"));

            var calloutOut = Clean("<div class=\"kh-callout\" style=\"background:#eff6ff;padding:16px;\">" +
                                   "<p>Nota</p></div>", HtmlCleanupLevel.Strict);
            Check("Nivel 2: conserva el fondo de los callouts marcados",
                calloutOut.Contains("background") && calloutOut.Contains("kh-callout"));
            var pastedDiv = Clean("<div style=\"background:#1e1e1e;color:#fff;\">pegado</div>", HtmlCleanupLevel.Strict);
            Check("Nivel 2: un div pegado SIN marca sí pierde el fondo",
                !pastedDiv.Contains("background") && pastedDiv.Contains("pegado"));

            // Trampa 2: aplanar no debe pegar los textos de bloques distintos.
            var plainOut = Clean("<h2>Titulo</h2><p>Parrafo uno</p><p>Parrafo <b>dos</b></p>" +
                                 "<img src=\"docimg://019f8d8a-f05a-7796-b5bf-ee2e48d67c29\"><ul><li>a</li><li>b</li></ul>",
                HtmlCleanupLevel.PlainText);
            Check("Nivel 3: no pega los párrafos entre sí",
                !plainOut.Contains("TituloParrafo") && !plainOut.Contains(">ab<") && !plainOut.EndsWith("ab"));
            Check("Nivel 3: conserva las imágenes y quita el resto de etiquetas",
                plainOut.Contains("docimg://") &&
                !plainOut.Contains("<h2", StringComparison.OrdinalIgnoreCase) &&
                !plainOut.Contains("<ul", StringComparison.OrdinalIgnoreCase) &&
                !plainOut.Contains("<b>", StringComparison.OrdinalIgnoreCase));

            // Trampa 3: con KeepChildNodes el texto de script/style se colaría al resultado.
            const string withScript = "<p>ok</p><script>alert(1)</script><style>p{color:red}</style>";
            Check("Niveles 2 y 3: el texto de script/style no se filtra",
                !Clean(withScript, HtmlCleanupLevel.Strict).Contains("alert") &&
                !Clean(withScript, HtmlCleanupLevel.Strict).Contains("color:red") &&
                !Clean(withScript, HtmlCleanupLevel.PlainText).Contains("alert") &&
                !Clean(withScript, HtmlCleanupLevel.PlainText).Contains("color:red"));

            // El nivel 1 no cambia: es el que usa el guardado.
            var std = Clean("<p style=\"color:#f00\">x</p><script>alert(1)</script>", HtmlCleanupLevel.Standard);
            Check("Nivel 1 sigue siendo el permisivo (conserva estilos, quita scripts)",
                std.Contains("color") && !std.Contains("alert"));

            // De la idempotencia dependen el «Nada que limpiar» y el log de SanitizeForSave.
            const string mixed = "<h2>T <span style=\"color:#fff\">i</span></h2><p>uno</p>" +
                                 "<img src=\"docimg://019f8d8a-f05a-7796-b5bf-ee2e48d67c29\" style=\"width:10px\">";
            var idempotent = true;
            foreach (var level in new[] { HtmlCleanupLevel.Standard, HtmlCleanupLevel.Strict, HtmlCleanupLevel.PlainText })
            {
                var once = Clean(mixed, level);
                if (Clean(once, level) != once) idempotent = false;
            }
            Check("Los tres niveles son idempotentes", idempotent);
        }

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

    /// <summary>Titles of the children of the page with that slug, in the order the tree shows them.</summary>
    private static async Task<List<string>> SiblingOrderAsync(IKnowledgeHubPageService pages, string parentSlug)
    {
        var tree = await pages.GetTreeAsync();
        if (!tree.OkNotNull) return new List<string>();
        var parent = FindBySlug(tree.Value!, parentSlug);
        return parent?.Children.Select(c => c.Title).ToList() ?? new List<string>();
    }

    /// <summary>True when the children's SortOrder is exactly 1..N with no gaps and no repeats.</summary>
    private static async Task<bool> SiblingsAreContiguousAsync(IKnowledgeHubPageService pages, string parentSlug)
    {
        var tree = await pages.GetTreeAsync();
        if (!tree.OkNotNull) return false;
        var parent = FindBySlug(tree.Value!, parentSlug);
        if (parent is null) return false;

        var orders = parent.Children.Select(c => c.SortOrder).ToList();
        return orders.SequenceEqual(Enumerable.Range(1, orders.Count));
    }

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
