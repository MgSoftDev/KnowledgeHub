using MgSoftDev.KnowledgeHub;
using MgSoftDev.KnowledgeHub.Contracts;
using MgSoftDev.KnowledgeHub.Dtos;
using MgSoftDev.KnowledgeHub.Enums;
using MgSoftDev.KnowledgeHub.Pdf;
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

    /// <summary>Checks that could not run on this machine (hoy: el motor real de PDF sin navegador).</summary>
    private static int _omitted;

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
        _omitted = 0;

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

        // ---- 16. Slug: títulos repetidos NO se bloquean --------------------------------------------
        // Antes esto se rechazaba con "Ya existe una página con ese slug", lo que impedía documentar
        // dos aplicaciones en el mismo árbol (cada una con su "Empezar"). Ahora el servicio deriva el
        // slug del título y le pone sufijo.
        user.SetUser("editor1", "Editor", KnowledgeHubPermissions.Edit);
        var dupSlug = await pages.CreatePageAsync(null, "Otra", "manual-usuario");
        Check("Slug explícito duplicado ya NO se rechaza", dupSlug.OkNotNull);
        var dupInfo = await pages.GetPageInfoAsync(dupSlug.Value);
        Check("Slug explícito duplicado recibe sufijo -2",
            dupInfo.OkNotNull && dupInfo.Value.Slug == "manual-usuario-2");

        // El caso real reportado: dos apps distintas, cada una con su página "Empezar".
        var appA = await pages.CreatePageAsync(null, "Line Management System");
        var appB = await pages.CreatePageAsync(null, "LineCtrlSys");
        var startA = await pages.CreatePageAsync(appA.Value, "Empezar");
        var startB = await pages.CreatePageAsync(appB.Value, "Empezar");
        Check("Mismo título bajo padres distintos: ambas se crean",
            startA.OkNotNull && startB.OkNotNull && startA.Value != startB.Value);

        var slugA = await pages.GetPageInfoAsync(startA.Value);
        var slugB = await pages.GetPageInfoAsync(startB.Value);
        Check("El slug se deriva del título y se desambigua",
            slugA.OkNotNull && slugA.Value.Slug == "empezar" &&
            slugB.OkNotNull && slugB.Value.Slug == "empezar-2");

        // Tercera vez seguida: el contador sigue, sin repetir ni saltarse un número.
        var startC = await pages.CreatePageAsync(appA.Value, "Empezar");
        var slugC = await pages.GetPageInfoAsync(startC.Value);
        Check("El tercer título repetido recibe -3",
            slugC.OkNotNull && slugC.Value.Slug == "empezar-3");

        // SlugExistsAsync cuenta también las borradas (a propósito: el índice único de la BD sigue
        // siendo global). Antes eso reservaba el título PARA SIEMPRE; ahora solo cuesta un sufijo.
        var recycled = await pages.CreatePageAsync(null, "Título Reciclado");
        await pages.DeletePageAsync(recycled.Value);
        var recreated = await pages.CreatePageAsync(null, "Título Reciclado");
        Check("Un título borrado se puede volver a usar", recreated.OkNotNull);

        Check("Slugify quita acentos y símbolos",
            KnowledgeHubSlug.Slugify("Línea 1 — Producción") == "linea-1-produccion");
        Check("Un título sin caracteres útiles no produce un slug vacío",
            KnowledgeHubSlug.Slugify("★ ★ ★") == KnowledgeHubSlug.Fallback);

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
        // Desde la 0.16.0 estas rutas responden el mensaje AMBIGUO: el mismo tanto si la página no
        // existe como si existe y no la ves. Distinguirlas convertía el rechazo en una forma de
        // enumerar páginas probando Guids.
        var permsMissing = await pages.GetPermissionsAsync(missingPk);
        Check("Permisos de página inexistente → Unfinished",
            IsUnfinishedContaining(permsMissing, "no existe o no tienes permiso"));
        var editMissing = await pages.GetPageForEditAsync(missingPk);
        Check("Editar página inexistente → Unfinished",
            IsUnfinishedContaining(editMissing, "no existe o no tienes permiso"));
        // Desde la 0.15.0 responde el mensaje AMBIGUO, el mismo que si existiera y no la vieras:
        // distinguir ambos casos convertía el rechazo en una forma de enumerar páginas probando Guids.
        var versionMissing = await pages.GetVersionContentAsync(missingPk);
        Check("Versión inexistente → Unfinished", IsUnfinishedContaining(versionMissing, "no existe o no tienes permiso"));

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

            // Pegado real de una web (reportado por el usuario). La lista de CSS de los niveles 2 y
            // 3 es de INCLUSIÓN justo por esto: con una de exclusión, propiedades que a nadie se le
            // ocurre listar (orphans, -webkit-*) pasaban enteras.
            const string webPaste =
                "<p style=\"line-height: inherit; orphans: 4; margin: 0px 10px; white-space: pre-wrap; " +
                "caret-color: rgb(0, 243, 255); color: rgb(126, 140, 159); font-family: Optima-Regular, " +
                "Optima, Cambria, serif; word-spacing: 2px; letter-spacing: 1.1px;\">Marcadora</p>" +
                "<p style=\"orphans: 4; color: rgb(126, 140, 159);\">Para esta semana</p>";
            var webStrict = Clean(webPaste, HtmlCleanupLevel.Strict);
            Check("Nivel 2: solo deja CSS estructural (ni orphans ni caret-color)",
                !webStrict.Contains("orphans") && !webStrict.Contains("caret-color") &&
                !webStrict.Contains("color") && !webStrict.Contains("Optima") &&
                webStrict.Contains("margin") && webStrict.Contains("Marcadora"));

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

            // Vaciar AllowedTags NO basta: 'style' seguía permitido con las 239 propiedades de
            // fábrica, así que el <p> pegado conservaba colores y fuentes enteros.
            Check("Nivel 3: el párrafo pegado queda SIN atributos",
                Clean(webPaste, HtmlCleanupLevel.PlainText) ==
                "<p>Marcadora</p><p>Para esta semana</p>");

            // Renombrar un contenedor a <p> anidaba párrafos y el parser los partía en vacíos.
            Check("Nivel 3: un contenedor con párrafos dentro no deja <p> vacíos",
                Clean("<div class=\"kh-callout\" style=\"background:#eff6ff;\"><p>Nota</p></div>",
                    HtmlCleanupLevel.PlainText) == "<p>Nota</p>");

            var plainImg = Clean("<img src=\"docimg://019f8d8a-f05a-7796-b5bf-ee2e48d67c29\" alt=\"QR\" " +
                                 "style=\"zoom:900%;width:100px;border:2px solid red;\">", HtmlCleanupLevel.PlainText);
            Check("Nivel 3: la imagen conserva su tamaño pero pierde lo cosmético",
                plainImg.Contains("zoom") && plainImg.Contains("100px") && plainImg.Contains("alt=\"QR\"") &&
                !plainImg.Contains("border"));

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

            // ---- Clases declaradas por el anfitrión ------------------------------------------
            // El caso real: una maquetación propia del anfitrión (un índice de módulos con sus
            // tarjetas) marcada con clases suyas. Antes solo sobrevivía kh-callout, así que el
            // guardado se llevaba la maquetación por delante sin decir nada.
            foreach (var level in new[] { HtmlCleanupLevel.Standard, HtmlCleanupLevel.Strict })
            {
                Check($"Nivel {(level == HtmlCleanupLevel.Standard ? 1 : 2)}: sobrevive la clase declarada por nombre",
                    Clean($"<div class=\"{HarnessSanitizer.NamedClass}\">x</div>", level)
                        .Contains(HarnessSanitizer.NamedClass));

                // El prefijo existe porque enumerar una familia que crece es una lista que alguien
                // olvidará: la clase nueva es indistinguible de basura y muere al guardar.
                Check($"Nivel {(level == HtmlCleanupLevel.Standard ? 1 : 2)}: sobrevive la familia declarada por prefijo",
                    Clean($"<div class=\"{HarnessSanitizer.Prefix}icon--orders\">x</div>", level)
                        .Contains($"{HarnessSanitizer.Prefix}icon--orders"));

                // Igualdad EXACTA: comprobar qué etiquetas sobreviven no dice nada de los
                // atributos, que es justo donde se escondió el fallo del nivel 3 en la 0.7.0.
                Check($"Nivel {(level == HtmlCleanupLevel.Standard ? 1 : 2)}: de la mezcla queda la mía y muere la de Word",
                    Clean($"<p class=\"{HarnessSanitizer.Prefix}card MsoNormal\">Texto</p>", level) ==
                    $"<p class=\"{HarnessSanitizer.Prefix}card\">Texto</p>");
            }

            // La regresión que importa: declarar clases propias NO reabre la puerta a Word, porque
            // AllowedClasses es una lista de INCLUSIÓN. Si algún día se vaciara, pasaría todo.
            Check("Declarar clases propias no deja pasar la basura de Word",
                Clean("<p class=\"MsoNormal\">Texto</p>", HtmlCleanupLevel.Standard) == "<p>Texto</p>");

            // El prefijo compara literal: 'kh-mi' no es 'kh-mi-'.
            Check("El prefijo no cuela clases que solo se le parecen",
                Clean("<p class=\"kh-mi\">x</p>", HtmlCleanupLevel.Standard) == "<p>x</p>");

            // El nivel 2 salva la clase, no la cosmética: el aspecto debe venir del CSS del
            // anfitrión, que es lo que hace que el nivel 2 siga sirviendo para algo.
            var declaredStrict = Clean(
                $"<div class=\"{HarnessSanitizer.Prefix}card\" style=\"background:#1e1e1e;color:#fff;\">x</div>",
                HtmlCleanupLevel.Strict);
            Check("Nivel 2: la clase declarada se salva pero su cosmética en línea no",
                declaredStrict.Contains($"{HarnessSanitizer.Prefix}card") &&
                !declaredStrict.Contains("background"));

            // El nivel 3 es «solo texto» y eso incluye las clases del anfitrión: ahí el atributo
            // cae entero porque AllowedAttributes se sustituye, no por la lista de clases.
            Check("Nivel 3 sigue quitando TODAS las clases, también las declaradas",
                Clean($"<p class=\"{HarnessSanitizer.NamedClass} {HarnessSanitizer.Prefix}card\">Texto</p>",
                    HtmlCleanupLevel.PlainText) == "<p>Texto</p>");
        }

        // ---- 25. Exportación a PDF ------------------------------------------------------------------------
        // Lo que se comprueba aquí NO es que el PDF sea bonito, sino que la exportación no pueda
        // convertirse en una fuga: lo que acaba en el archivo es exactamente lo que ese usuario
        // podría abrir a mano, página por página.
        // Se toma del proveedor del SERVIDOR: la seguridad vive en el core y es la misma en los
        // cuatro modos. Sobre HTTP se comprueba aparte que el endpoint devuelve el binario.
        var serverProvider = seederProvider ?? sp;
        var export = serverProvider.GetService<IKnowledgeHubPdfExportService>();
        var options = serverProvider.GetService<KnowledgeHubOptions>();
        Check("Servicio de exportación registrado", export is not null && options is not null);

        if (export is not null && options is not null)
        {
            user.SetUser("admin", "Administrador", KnowledgeHubPermissions.Admin);

            // Rama de tres niveles: la del medio solo la ve quien tenga Docs.Tech.
            var expRoot = await pages.CreatePageAsync(null, "Exportar Raíz", "exportar-raiz");
            var expChild = await pages.CreatePageAsync(expRoot.Value, "Exportar Hija", "exportar-hija");
            var expDraft = await pages.CreatePageAsync(expRoot.Value, "Exportar Borrador", "exportar-borrador");
            foreach (var pk in new[] { expRoot.Value, expChild.Value })
            {
                await PublishSimpleAsync(pages, pk, $"<p>Contenido de {pk}</p>");
            }
            // La raíz pública y la hija restringida: así se puede comprobar que un lector recibe
            // la rama RECORTADA, en vez de que se le niegue entera y no se pruebe nada.
            await pages.SetPermissionsAsync(expRoot.Value, true, Array.Empty<string>());
            await pages.SetPermissionsAsync(expChild.Value, false, new[] { "Docs.Tech" });

            var onlyRoot = await export.BuildAsync(expRoot.Value, includeDescendants: false);
            Check("Exportar una página da UNA sección",
                onlyRoot.OkNotNull && onlyRoot.Value.Sections.Count == 1 &&
                onlyRoot.Value.Sections[0].Title == "Exportar Raíz");

            var branch = await export.BuildAsync(expRoot.Value, includeDescendants: true);
            Check("La rama incluye a la hija publicada y NO al borrador",
                branch.OkNotNull && branch.Value.Sections.Count == 2 &&
                branch.Value.Sections.Any(s => s.Title == "Exportar Hija") &&
                branch.Value.Sections.All(s => s.Title != "Exportar Borrador"));
            Check("Los niveles reflejan la jerarquía",
                branch.OkNotNull && branch.Value.Sections.First(s => s.Title == "Exportar Raíz").Level == 1 &&
                branch.Value.Sections.First(s => s.Title == "Exportar Hija").Level == 2);

            // El corazón del asunto: un usuario que no ve la hija no puede sacarla en el PDF.
            user.SetUser("lector", "Lector sin permisos");
            var limited = await export.BuildAsync(expRoot.Value, includeDescendants: true);
            Check("Un usuario sin visibilidad NO recibe la página restringida",
                limited.OkNotNull && limited.Value.Sections.Count == 1 &&
                limited.Value.Sections.All(s => s.Title != "Exportar Hija"));

            var deniedPage = await export.BuildAsync(expChild.Value, includeDescendants: false);
            Check("Exportar directamente una página que no ve se rechaza",
                IsUnfinishedContaining(deniedPage, "permiso"));

            var notPublished = await export.BuildAsync(expDraft.Value, includeDescendants: false);
            Check("Una página sin publicar no se exporta", !notPublished.OkNotNull);

            // Opt-in del permiso: apagado cualquiera exporta lo que puede leer; encendido hace falta.
            options.UseFineGrainedExport = true;
            var noPermission = await export.BuildAsync(expRoot.Value, includeDescendants: false);
            Check("Con UseFineGrainedExport, sin el permiso se rechaza",
                IsUnfinishedContaining(noPermission, "permiso"));

            user.SetUser("descargador", "Con permiso", KnowledgeHubPermissions.Export);
            var withPermission = await export.BuildAsync(expRoot.Value, includeDescendants: false);
            Check("Con el permiso Export sí exporta", withPermission.OkNotNull);
            options.UseFineGrainedExport = false;

            // El tope rechaza, nunca trunca: media exportación que parece entera es peor que un error.
            user.SetUser("admin", "Administrador", KnowledgeHubPermissions.Admin);
            options.MaxExportPages = 1;
            var overLimit = await export.BuildAsync(expRoot.Value, includeDescendants: true);
            Check("Superar MaxExportPages rechaza en vez de truncar",
                IsUnfinishedContaining(overLimit, "límite"));
            options.MaxExportPages = 200;

            // El renderer se busca en el SERVIDOR, no en sp: sobre HTTP el cliente no lo tiene y
            // estos checks se saltarían en silencio, dejando ese modo con menos cobertura sin que
            // se notara en el recuento.
            Check("Renderer de PDF registrado", serverProvider.GetService<IKnowledgeHubPdfRenderer>() is not null);

            // El arnés registra un renderer FALSO: el motor real es Chromium y dependería de que la
            // máquina tenga navegador, lo que haría que estos checks dieran distinto según dónde se
            // ejecute. Lo que se verifica aquí es el pipeline y los permisos, no el dibujo.
            var file = await export.ExportAsync(expRoot.Value, includeDescendants: true);
            Check("ExportAsync devuelve bytes de PDF",
                file.OkNotNull && file.Value.Content.Length > 1024 && IsPdf(file.Value.Content));
            Check("El nombre del archivo sale del título",
                file.OkNotNull && file.Value.FileName == "exportar-raiz.pdf");

            // Y por el camino del cliente: en los modos en proceso es el mismo servicio, pero
            // sobre HTTP esto atraviesa el endpoint binario de verdad.
            var clientExport = sp.GetService<IKnowledgeHubPdfExportService>();
            var overTransport = clientExport is null
                ? file
                : await clientExport.ExportAsync(expRoot.Value, includeDescendants: true);
            Check("La exportación llega igual por el transporte del cliente",
                overTransport.OkNotNull && IsPdf(overTransport.Value.Content) &&
                overTransport.Value.FileName == "exportar-raiz.pdf");

            // ---- Páginas excluidas y páginas vacías ------------------------------------------
            // El caso real: una página que es un tablero de enlaces, útil para navegar e inútil
            // impresa. La marca excluye ESA página, nunca su rama.
            var pdfRoot = await pages.CreatePageAsync(null, "PDF Manual", "pdf-manual");
            var pdfBoard = await pages.CreatePageAsync(pdfRoot.Value, "PDF Tablero", "pdf-tablero");
            var pdfLeaf = await pages.CreatePageAsync(pdfBoard.Value, "PDF Hoja", "pdf-hoja");
            var pdfEmpty = await pages.CreatePageAsync(pdfRoot.Value, "PDF Vacia", "pdf-vacia");
            var pdfImage = await pages.CreatePageAsync(pdfRoot.Value, "PDF Imagen", "pdf-imagen");

            await PublishSimpleAsync(pages, pdfRoot.Value, "<p>Portada del manual</p>");
            await PublishSimpleAsync(pages, pdfBoard.Value, "<p>Enlaces</p>");
            await PublishSimpleAsync(pages, pdfLeaf.Value, "<p>Contenido real</p>");
            // Lo que deja el editor cuando se crea una página y no se escribe nada.
            await PublishSimpleAsync(pages, pdfEmpty.Value, "<p><br></p>");
            // Sin una sola letra, pero se ve: no puede tratarse como vacía.
            await PublishSimpleAsync(pages, pdfImage.Value,
                "<p><img src=\"docimg://019f8d8a-f05a-7796-b5bf-ee2e48d67c29\" alt=\"Q\"></p>");
            foreach (var pk in new[] { pdfRoot.Value, pdfBoard.Value, pdfLeaf.Value, pdfEmpty.Value, pdfImage.Value })
                await pages.SetPermissionsAsync(pk, true, Array.Empty<string>());

            var beforeMark = await export.BuildAsync(pdfRoot.Value, includeDescendants: true);
            Check("Sin marcar, el tablero entra en la rama",
                beforeMark.OkNotNull && beforeMark.Value.Sections.Any(s => s.Title == "PDF Tablero"));
            Check("Una página vacía NO genera sección",
                beforeMark.OkNotNull && beforeMark.Value.Sections.All(s => s.Title != "PDF Vacia"));
            Check("Una página de solo imagen SÍ se exporta",
                beforeMark.OkNotNull && beforeMark.Value.Sections.Any(s => s.Title == "PDF Imagen"));

            Check("Marcar la página como no exportable",
                (await pages.SetPageExcludeFromPdfAsync(pdfBoard.Value, true)).Ok);
            var markedInfo = await pages.GetPageInfoAsync(pdfBoard.Value);
            Check("La marca se lee de vuelta", markedInfo.OkNotNull && markedInfo.Value.ExcludeFromPdf);

            var branchAfter = await export.BuildAsync(pdfRoot.Value, includeDescendants: true);
            Check("La rama se salta el tablero pero conserva su hoja",
                branchAfter.OkNotNull &&
                branchAfter.Value.Sections.All(s => s.Title != "PDF Tablero") &&
                branchAfter.Value.Sections.Any(s => s.Title == "PDF Hoja"));

            var boardAlone = await export.BuildAsync(pdfBoard.Value, includeDescendants: false);
            Check("Exportar solo la página marcada se rechaza diciendo por qué",
                IsUnfinishedContaining(boardAlone, "no exportable"));

            var boardBranch = await export.BuildAsync(pdfBoard.Value, includeDescendants: true);
            Check("Desde la marcada, «con sus subpáginas» sí saca a las hijas",
                boardBranch.OkNotNull && boardBranch.Value.Sections.Count == 1 &&
                boardBranch.Value.Sections[0].Title == "PDF Hoja");

            // La excluida NO gasta cupo, porque se filtra al planificar; la vacía sí, porque su
            // contenido no se conoce hasta leerla. Quedan 4 planificadas: sin la marca serían 5 y
            // este tope las rechazaría.
            options.MaxExportPages = 4;
            var quota = await export.BuildAsync(pdfRoot.Value, includeDescendants: true);
            Check("Una página excluida no consume cupo de MaxExportPages", quota.OkNotNull);
            options.MaxExportPages = 200;

            // El helper directo, con las dos trampas que hacen que no sea "html vacío".
            Check("IsVisuallyEmpty: nada, solo etiquetas o solo &nbsp;",
                KnowledgeHubHtml.IsVisuallyEmpty(null) && KnowledgeHubHtml.IsVisuallyEmpty("") &&
                KnowledgeHubHtml.IsVisuallyEmpty("<p><br></p>") &&
                KnowledgeHubHtml.IsVisuallyEmpty("<p>&nbsp;</p>") &&
                KnowledgeHubHtml.IsVisuallyEmpty("<div><p></p></div>"));
            // La librería añade <p><br></p> al final de CADA callout: si eso contara como vacío,
            // un documento que solo tiene un aviso desaparecería del PDF.
            Check("IsVisuallyEmpty: un callout no está vacío aunque acabe en <p><br></p>",
                !KnowledgeHubHtml.IsVisuallyEmpty(
                    "<div class=\"kh-callout\"><p><strong>Nota:</strong> ojo</p></div><p><br></p>"));
            Check("IsVisuallyEmpty: una imagen sola no está vacía",
                !KnowledgeHubHtml.IsVisuallyEmpty(
                    "<p><img src=\"docimg://019f8d8a-f05a-7796-b5bf-ee2e48d67c29\"></p>"));

            // Y una pasada con el motor REAL, que sí necesita navegador. Si no lo hay se informa
            // en voz alta: un salto silencioso haría creer que el motor quedó probado.
            await CheckRealPdfEngineAsync(export, expRoot.Value);
        }

        // ---- 26. Datos vivos en las páginas -------------------------------------------------------------
        // Lo que se comprueba: que solo se procesen las páginas marcadas (o se romperían los
        // ejemplos de código que usan llaves), que el resultado se sanee (Scriban no escapa nada),
        // y que un bloque condicionado por rol no se filtre a quien no lo tiene.
        user.SetUser("admin", "Administrador", KnowledgeHubPermissions.Admin);

        var tplRoot = await pages.CreatePageAsync(null, "Plantillas");
        await pages.SetPermissionsAsync(tplRoot.Value, true, Array.Empty<string>());

        var marcada = await pages.CreatePageAsync(tplRoot.Value, "Con datos vivos");
        var sinMarcar = await pages.CreatePageAsync(tplRoot.Value, "Sin datos vivos");
        foreach (var pk in new[] { marcada.Value, sinMarcar.Value })
            await pages.SetPermissionsAsync(pk, true, Array.Empty<string>());

        var marcarR = await pages.SetPageUsesTemplatesAsync(marcada.Value, true);
        Check("Un admin puede marcar la página como dinámica", marcarR.Ok);

        // Que RENDERICE no prueba que la pantalla de Gestionar lo lea de vuelta: son dos caminos
        // distintos y el DTO se rellena a mano, campo a campo. Faltaba este campo y la casilla
        // aparecía siempre desmarcada aunque la página sí procesara sus datos.
        var infoMarcada = await pages.GetPageInfoAsync(marcada.Value);
        Check("La marca vuelve en GetPageInfo (es lo que pinta la casilla)",
            infoMarcada.OkNotNull && infoMarcada.Value.UsesTemplates);

        const string plantilla = "<ul>{{ for e in equipos }}<li>{{ e.nombre }} {{ e.ip }}</li>{{ end }}</ul>";
        await PublishSimpleAsync(pages, marcada.Value, plantilla);
        await PublishSimpleAsync(pages, sinMarcar.Value, plantilla);

        var renderizada = await pages.GetPageForReadAsync(marcada.Value);
        Check("La página marcada se rellena con los datos del proveedor",
            renderizada.OkNotNull && renderizada.Value.ContentHtml.Contains("PC-1") &&
            renderizada.Value.ContentHtml.Contains("10.0.0.2") &&
            !renderizada.Value.ContentHtml.Contains("{{"),
            renderizada.OkNotNull ? renderizada.Value.ContentHtml : "no se pudo leer");

        // La regresión que protege todo lo ya escrito: sin marcar, las llaves se quedan como están.
        var literal = await pages.GetPageForReadAsync(sinMarcar.Value);
        Check("Sin marcar, las llaves salen literales y no se procesa nada",
            literal.OkNotNull && literal.Value.ContentHtml.Contains("{{ for e in equipos }}"));

        // El editor SIEMPRE ve la plantilla, nunca su resultado: si no, editar la destruiría.
        var paraEditar = await pages.GetPageForEditAsync(marcada.Value);
        Check("El editor recibe la plantilla sin renderizar",
            paraEditar.OkNotNull && paraEditar.Value.ContentHtml.Contains("{{ for e in equipos }}"));

        // Scriban no escapa nada, así que esto es lo único entre una API ajena y el navegador.
        await PublishSimpleAsync(pages, marcada.Value,
            "<p>{{ for e in equipos }}{{ e.peligroso }}{{ end }}</p>");
        var saneada = await pages.GetPageForReadAsync(marcada.Value);
        Check("La salida renderizada se sanea (el script del dato no sobrevive)",
            saneada.OkNotNull && !saneada.Value.ContentHtml.Contains("<script"),
            saneada.OkNotNull ? saneada.Value.ContentHtml : "no se pudo leer");

        // kh.is_pdf: el MISMO contenido tiene que dar dos resultados distintos.
        await PublishSimpleAsync(pages, marcada.Value,
            "<p>Siempre</p>{{ if !kh.is_pdf }}<p>SOLO-PANTALLA</p>{{ end }}");
        var enPantalla = await pages.GetPageForReadAsync(marcada.Value);
        Check("kh.is_pdf es falso en el lector",
            enPantalla.OkNotNull && enPantalla.Value.ContentHtml.Contains("SOLO-PANTALLA"));

        if (export is not null)
        {
            var enPdf = await export.BuildAsync(marcada.Value, includeDescendants: false);
            Check("kh.is_pdf es verdadero al exportar: el bloque desaparece del PDF",
                enPdf.OkNotNull && enPdf.Value.Sections.Count == 1 &&
                !enPdf.Value.Sections[0].ContentHtml.Contains("SOLO-PANTALLA") &&
                enPdf.Value.Sections[0].ContentHtml.Contains("Siempre"),
                enPdf.OkNotNull ? enPdf.Value.Sections[0].ContentHtml : "no se pudo construir");
        }

        // Un bloque por rol: la misma página, dos usuarios, dos salidas. Es un check de FUGA.
        await PublishSimpleAsync(pages, marcada.Value,
            "<p>Público</p>{{ if kh.user.has \"Docs.Tech\" }}<p>SECRETO</p>{{ end }}");

        // Con el rol de contenido, NO con Admin: kh.user.has mira los permisos del anfitrión, y ser
        // administrador no te mete en Docs.Tech.
        user.SetUser("tecnico", "Técnico", KnowledgeHubPermissions.Edit, "Docs.Tech");
        var conRol = await pages.GetPageForReadAsync(marcada.Value);
        Check("Con el rol, el bloque condicionado se ve",
            conRol.OkNotNull && conRol.Value.ContentHtml.Contains("SECRETO"),
            conRol.OkNotNull ? conRol.Value.ContentHtml : "no se pudo leer");

        user.SetUser("lector", "Lector sin permisos");
        var sinRol = await pages.GetPageForReadAsync(marcada.Value);
        Check("Sin el rol, el bloque condicionado NO llega al usuario",
            sinRol.OkNotNull && !sinRol.Value.ContentHtml.Contains("SECRETO") &&
            sinRol.Value.ContentHtml.Contains("Público"));

        // Sin el permiso no se marca, aunque se pueda editar.
        user.SetUser("editor", "Editor", KnowledgeHubPermissions.Edit);
        var sinPermiso = await pages.SetPageUsesTemplatesAsync(sinMarcar.Value, true);
        Check("Sin el permiso Templates no se puede marcar una página",
            IsUnfinishedContaining(sinPermiso, "permiso"));

        user.SetUser("admin", "Administrador", KnowledgeHubPermissions.Admin);

        // Publicar RECHAZA una plantilla rota; guardar el borrador SÍ funciona. La asimetría es
        // deliberada: un borrador no lo ve nadie, y bloquearlo dejaría al autor a media plantilla.
        var editRota = await pages.GetPageForEditAsync(marcada.Value);
        editRota.Value.ContentHtml = "<p>{{ for e in equipos }}</p>";
        var guardadaRota = await pages.SaveDraftAsync(editRota.Value);
        Check("Guardar una plantilla rota SÍ funciona (es un borrador)", guardadaRota.OkNotNull);

        var publicarRota = await pages.PublishAsync(marcada.Value, guardadaRota.Value);
        Check("Publicar una plantilla rota se RECHAZA",
            IsUnfinishedContaining(publicarRota, "plantilla"));
        Check("El rechazo al publicar dice la línea",
            !publicarRota.Ok && (publicarRota.UnfinishedInfo?.Mensaje ?? "").Contains("línea"));

        var validada = await pages.ValidateTemplateAsync("<p>{{ for e in equipos }}</p>");
        Check("La validación devuelve el error con su línea",
            validada.OkNotNull && validada.Value.Count > 0 && validada.Value[0].Line >= 1);
        Check("Una plantilla correcta valida sin errores",
            await pages.ValidateTemplateAsync(plantilla) is { OkNotNull: true, Value.Count: 0 });

        // Un proveedor que revienta cuesta su variable, no la página.
        await PublishSimpleAsync(pages, marcada.Value, "<p>Antes</p>" + plantilla + "<p>Después</p>");
        HarnessTemplateModelProvider.Fallar = true;
        var conProveedorRoto = await pages.GetPageForReadAsync(marcada.Value);
        HarnessTemplateModelProvider.Fallar = false;
        Check("Un proveedor que falla no tumba la página",
            conProveedorRoto.OkNotNull && conProveedorRoto.Value.ContentHtml.Contains("Antes") &&
            conProveedorRoto.Value.ContentHtml.Contains("Después"));

        // Al desmarcar hay que volver a limpiar: mientras estuvo marcada, las regiones {{ }} pasaron
        // el saneador sin mirarse, y ya no queda nadie que las consuma.
        var desmarcar = await pages.SetPageUsesTemplatesAsync(marcada.Value, false);
        Check("Se puede desmarcar la página", desmarcar.Ok);

        var infoDesmarcada = await pages.GetPageInfoAsync(marcada.Value);
        Check("Al desmarcar, GetPageInfo también lo refleja",
            infoDesmarcada.OkNotNull && !infoDesmarcada.Value.UsesTemplates);
        var trasDesmarcar = await pages.GetPageForReadAsync(marcada.Value);
        Check("Tras desmarcar, ya no se renderiza",
            trasDesmarcar.OkNotNull && trasDesmarcar.Value.ContentHtml.Contains("{{"));

        // ---- 26. El historial ya no es una puerta trasera ------------------------------------------------
        // Salió de una pregunta sobre enlaces: el cuerpo de una página deja a la vista el Guid de la
        // página enlazada, y con ese Guid se podía pedir la lista de versiones y luego el HTML de
        // cada una SIN NINGÚN permiso. El árbol, la búsqueda, la lectura y el PDF filtraban; el
        // historial no. Estos checks son la prueba de que esa puerta está cerrada.
        user.SetUser("admin", "Administrador", KnowledgeHubPermissions.Admin);
        var secret = await pages.CreatePageAsync(null, "Cifras de dirección", "cifras-direccion");
        await PublishSimpleAsync(pages, secret.Value, "<p>Margen por línea: confidencial</p>");
        await pages.SetPermissionsAsync(secret.Value, false, new[] { "Docs.Tech" });

        var secretVersions = await pages.GetVersionsAsync(secret.Value);
        Check("Admin sí lista el historial", secretVersions.Ok && secretVersions.Value!.Count > 0);
        var secretVersionPk = secretVersions.OkNotNull && secretVersions.Value!.Count > 0
            ? secretVersions.Value![0].Pk
            : Guid.NewGuid();

        // Un editor CON permiso de edición pero SIN visibilidad sobre esta página: es el caso que
        // devolvía el HTML entero.
        user.SetUser("editor-sin-acceso", "Editor", KnowledgeHubPermissions.Edit);
        var deniedList = await pages.GetVersionsAsync(secret.Value);
        Check("Editor sin visibilidad NO lista el historial",
            IsUnfinishedContaining(deniedList, "no existe o no tienes permiso"));
        var deniedContent = await pages.GetVersionContentAsync(secretVersionPk);
        Check("Editor sin visibilidad NO lee el contenido de una versión",
            IsUnfinishedContaining(deniedContent, "no existe o no tienes permiso"));
        Check("El contenido confidencial no viaja en el rechazo",
            !deniedContent.OkNotNull);

        // Y el rechazo tiene que ser INDISTINGUIBLE del de una página que no existe, o el propio
        // mensaje sirve para enumerar Guids.
        var ghostContent = await pages.GetVersionContentAsync(Guid.NewGuid());
        Check("El rechazo no distingue «no existe» de «no puedes verla»",
            deniedContent.UnfinishedInfo?.Title == ghostContent.UnfinishedInfo?.Title);

        // Decisión explícita de la 0.15.0: el historial pasa a ser cosa de editores. Un lector que SÍ
        // ve la página tampoco entra. Es un cambio de comportamiento, y por eso se afirma.
        user.SetUser("lector-con-acceso", "Lector", "Docs.Tech");
        Check("Un lector con visibilidad pero sin edición ya no ve el historial",
            IsUnfinishedContaining(await pages.GetVersionsAsync(secret.Value), "permiso"));

        // Y lo que debe seguir funcionando: editor con las dos cosas.
        user.SetUser("editor-con-acceso", "Editor", KnowledgeHubPermissions.Edit, "Docs.Tech");
        Check("Editor con visibilidad sigue listando el historial",
            (await pages.GetVersionsAsync(secret.Value)).Ok);
        Check("Editor con visibilidad sigue leyendo la versión",
            (await pages.GetVersionContentAsync(secretVersionPk)).OkNotNull);

        // ---- 27. Una página nueva no se pierde --------------------------------------------------------
        // Reportado probando el demo: un editor creaba una página, la publicaba y NO la veía nunca
        // en el árbol. Una página nace sin permisos y no pública, así que bajo la regla vieja era
        // invisible para todos menos Admin — incluido su propio autor. Publicar no cambia nada de eso.
        user.SetUser("autor", "Autor sin roles de contenido", KnowledgeHubPermissions.Edit);
        var suya = await pages.CreatePageAsync(null, "Borrador del autor", "borrador-autor");
        Check("Crear una página de raíz", suya.Ok);

        var treeAutor = await pages.GetTreeAsync();
        Check("El autor SÍ ve en su árbol la página que acaba de crear",
            treeAutor.Ok && FindBySlug(treeAutor.Value!, "borrador-autor") is not null);
        Check("Y puede gestionarla y editarla",
            (await pages.GetPageInfoAsync(suya.Value)).OkNotNull &&
            (await pages.GetPageForEditAsync(suya.Value)).OkNotNull);

        // Sin configurar la ve cualquier editor —está oculta para todos, no protege a nadie— pero
        // NUNCA un lector.
        user.SetUser("otro-editor", "Otro editor", KnowledgeHubPermissions.Edit);
        var treeOtro = await pages.GetTreeAsync();
        Check("Otro editor también ve la página sin configurar",
            treeOtro.Ok && FindBySlug(treeOtro.Value!, "borrador-autor") is not null);

        user.SetUser("lector-raso", "Lector");
        var treeLector = await pages.GetTreeAsync();
        Check("Un lector NO ve una página sin configurar",
            treeLector.Ok && FindBySlug(treeLector.Value!, "borrador-autor") is null);

        // En cuanto se configura, manda la regla de siempre: deja de ser «sin configurar».
        user.SetUser("autor", "Autor sin roles de contenido", KnowledgeHubPermissions.Edit);
        await pages.SetPermissionsAsync(suya.Value, false, new[] { "Docs.Ofi" });
        var treeTrasConfigurar = await pages.GetTreeAsync();
        Check("Configurada para otro rol, el editor deja de verla",
            treeTrasConfigurar.Ok && FindBySlug(treeTrasConfigurar.Value!, "borrador-autor") is null);

        // Herencia: una subpágina nace con la audiencia de su padre, que es lo que evita el problema
        // en el caso habitual (crear dentro de una rama ya configurada).
        user.SetUser("admin", "Administrador", KnowledgeHubPermissions.Admin);
        var hijaHeredada = await pages.CreatePageAsync(secret.Value, "Detalle de cifras", "detalle-cifras");
        Check("Crear subpágina bajo una rama restringida", hijaHeredada.Ok);
        var permsHeredados = await pages.GetPermissionsAsync(hijaHeredada.Value);
        Check("La subpágina hereda los permisos del padre",
            permsHeredados.OkNotNull && permsHeredados.Value.Permissions.Contains("Docs.Tech"));

        user.SetUser("editor-con-acceso", "Editor", KnowledgeHubPermissions.Edit, "Docs.Tech");
        var treeHeredado = await pages.GetTreeAsync();
        Check("Quien ve al padre ve la subpágina heredada",
            treeHeredado.Ok && FindBySlug(treeHeredado.Value!, "detalle-cifras") is not null);

        user.SetUser("editor-otro-rol", "Editor de otra área", KnowledgeHubPermissions.Edit, "Docs.Ofi");
        var treeAjeno = await pages.GetTreeAsync();
        Check("Un editor de otra área NO ve la subpágina heredada",
            treeAjeno.Ok && FindBySlug(treeAjeno.Value!, "detalle-cifras") is null);

        // ---- 28. La escalada de privilegios, cerrada --------------------------------------------------
        // El editor de otra área no ve «Cifras de dirección». Con CanManagePermissions cayendo en
        // CanEdit, sin guarda podría hacerse público y leerlo por la puerta de delante.
        var escalada = await pages.SetPermissionsAsync(secret.Value, true, Array.Empty<string>());
        Check("Un editor sin visibilidad NO puede auto-concederse permisos",
            IsUnfinishedContaining(escalada, "no existe o no tienes permiso"));
        Check("Y la lectura sigue rechazando después del intento",
            !(await pages.GetPageForReadAsync(secret.Value)).OkNotNull);
        Check("Tampoco puede leer la ACL para saber qué rol pedirse",
            IsUnfinishedContaining(await pages.GetPermissionsAsync(secret.Value), "no existe o no tienes permiso"));

        // Gestión sobre una página que no ve: rechazada Y sin efecto (se afirma el efecto, no solo
        // el Returning).
        Check("No puede renombrar lo que no ve",
            IsUnfinishedContaining(await pages.RenamePageAsync(secret.Value, "Secuestrada"), "no existe o no tienes permiso"));
        Check("No puede borrar lo que no ve",
            IsUnfinishedContaining(await pages.DeletePageAsync(secret.Value), "no existe o no tienes permiso"));

        user.SetUser("admin", "Administrador", KnowledgeHubPermissions.Admin);
        var intacta = await pages.GetPageInfoAsync(secret.Value);
        Check("La página sigue intacta tras los intentos",
            intacta.OkNotNull && intacta.Value.Title == "Cifras de dirección");

        // ---- 29. Borrar en cascada no puede llevarse lo que no se ve ----------------------------------
        // El subárbol se calcula sobre la lista de enlaces SIN filtrar —tiene que ser así, o la
        // cascada dejaría huérfanos—, así que sin este chequeo un editor borra una rama y destruye
        // las páginas restringidas de debajo en silencio: no las ve, no puede enumerarlas y nadie
        // se lo dice. Es pérdida de datos, no un detalle de UX.
        var planta = await pages.CreatePageAsync(null, "Manual de Planta");
        await pages.SetPermissionsAsync(planta.Value, true, Array.Empty<string>());
        var costes = await pages.CreatePageAsync(planta.Value, "Costes internos");
        await pages.SetPermissionsAsync(costes.Value, false, new[] { "Docs.Dir" });

        user.SetUser("editor-otro-rol", "Editor de otra área", KnowledgeHubPermissions.Edit, "Docs.Ofi");
        var borradoCiego = await pages.DeletePageAsync(planta.Value);
        Check("No puede borrar una rama que esconde páginas que no ve",
            IsUnfinishedContaining(borradoCiego, "no tienes permiso para ver"));
        Check("El aviso nombra la página en el título",
            borradoCiego.UnfinishedInfo?.Title?.Contains("Manual de Planta") == true);
        // Primer uso en la librería de la sobrecarga de 3 argumentos de Unfinished: hasta ahora el
        // Mensaje viajaba vacío, así que se afirma sobre él y no solo sobre el título.
        Check("Y el mensaje dice CUÁNTAS son, en singular",
            borradoCiego.UnfinishedInfo?.Mensaje?.Contains("1 subpágina que no tienes permiso") == true);

        var ventas = await pages.CreatePageAsync(planta.Value, "Márgenes");
        user.SetUser("admin", "Administrador", KnowledgeHubPermissions.Admin);
        await pages.SetPermissionsAsync(ventas.Value, false, new[] { "Docs.Dir" });
        user.SetUser("editor-otro-rol", "Editor de otra área", KnowledgeHubPermissions.Edit, "Docs.Ofi");
        Check("Con dos escondidas el mensaje va en plural",
            (await pages.DeletePageAsync(planta.Value)).UnfinishedInfo?.Mensaje
                ?.Contains("2 subpáginas que no tienes permiso") == true);

        // Lo que de verdad importa no es el rechazo, sino que NO se borró nada.
        user.SetUser("admin", "Administrador", KnowledgeHubPermissions.Admin);
        Check("Tras el rechazo, la rama sigue entera",
            (await pages.GetPageInfoAsync(planta.Value)).OkNotNull &&
            (await pages.GetPageInfoAsync(costes.Value)).OkNotNull &&
            (await pages.GetPageInfoAsync(ventas.Value)).OkNotNull);

        // Que el chequeo no dé falsos positivos importa tanto como el bloqueo.
        var abierta = await pages.CreatePageAsync(null, "Rama abierta");
        await pages.SetPermissionsAsync(abierta.Value, true, Array.Empty<string>());
        var abiertaHija = await pages.CreatePageAsync(abierta.Value, "Hija heredada");
        var sinConfigurar = await pages.CreatePageAsync(abierta.Value, "Sin configurar");
        await pages.SetPermissionsAsync(sinConfigurar.Value, false, Array.Empty<string>());

        user.SetUser("editor-otro-rol", "Editor de otra área", KnowledgeHubPermissions.Edit, "Docs.Ofi");
        Check("Sí borra una rama cuyos descendientes ve todos, sin configurar incluida",
            (await pages.DeletePageAsync(abierta.Value)).Ok);

        user.SetUser("admin", "Administrador", KnowledgeHubPermissions.Admin);
        Check("Y esa rama desapareció entera",
            !(await pages.GetPageInfoAsync(abierta.Value)).OkNotNull &&
            !(await pages.GetPageInfoAsync(abiertaHija.Value)).OkNotNull &&
            !(await pages.GetPageInfoAsync(sinConfigurar.Value)).OkNotNull);

        Check("El admin sí puede borrar la rama con páginas restringidas",
            (await pages.DeletePageAsync(planta.Value)).Ok);
        Check("Y se llevó también las restringidas",
            !(await pages.GetPageInfoAsync(costes.Value)).OkNotNull &&
            !(await pages.GetPageInfoAsync(ventas.Value)).OkNotNull);

        Console.WriteLine();
        var omitted = _omitted > 0 ? $" / {_omitted} OMIT" : string.Empty;
        Console.WriteLine($"===== RESULTADO: {_passed} PASS / {_failed} FAIL{omitted} =====");
        return _failed;
    }

    // ---------------------------------------------------------------- Helpers

    // 'detail' se imprime SOLO al fallar. Merece la pena pasar lo que el check comparó —el html que
    // recibió, el mensaje que leyó—, porque una línea roja que solo dice lo que se esperaba te
    // obliga a volver a ejecutar el arnés con prints añadidos.
    private static void Check(string name, bool condition, string? detail = null)
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
            if (!string.IsNullOrWhiteSpace(detail)) Console.WriteLine($"         → {detail}");
        }
    }

    private static bool IsUnfinishedContaining(ReturningBase result, string text) =>
        !result.Ok && result.UnfinishedInfo is { } unfinished &&
        $"{unfinished.Title} {unfinished.Mensaje}".Contains(text, StringComparison.OrdinalIgnoreCase);

    /// <summary>Titles of the children of the page with that slug, in the order the tree shows them.</summary>
    private static bool IsPdf(byte[] bytes) =>
        bytes.Length > 4 && bytes[0] == '%' && bytes[1] == 'P' && bytes[2] == 'D' && bytes[3] == 'F';

    /// <summary>
    /// Runs the REAL engine once. Unlike everything else in this script it needs a browser on the
    /// machine, so it can legitimately not run — and when that happens it says so out loud and is
    /// counted apart. Reporting it as a pass, or skipping it in silence, would both suggest the
    /// engine had been exercised when it had not.
    /// </summary>
    private static async Task CheckRealPdfEngineAsync(IKnowledgeHubPdfExportService export, Guid rootPagePk)
    {
        var built = await export.BuildAsync(rootPagePk, includeDescendants: true);
        if (!built.OkNotNull)
        {
            Check("Motor real: se pudo construir el documento", false);
            return;
        }

        using var renderer = new PlaywrightPdfRenderer();
        var rendered = await renderer.RenderAsync(built.Value);

        if (!rendered.OkNotNull)
        {
            _omitted++;
            Console.WriteLine("  [OMIT] Motor real (Chromium) -> " +
                              (rendered.UnfinishedInfo?.Title ?? "no disponible en esta máquina"));
            return;
        }

        var pdf = rendered.Value;
        var raw = System.Text.Encoding.Latin1.GetString(pdf);
        Console.WriteLine($"         (motor real: {pdf.Length} bytes)");
        Check("Motor real (Chromium) produce un PDF", IsPdf(pdf));
        Check("El PDF lleva marcadores navegables (/Outlines)", raw.Contains("/Outlines"));
        Check("El índice deja enlaces internos (/Annots)", raw.Contains("/Annots"));
    }

    /// <summary>Saves a draft with the given html and publishes it, so the page becomes readable.</summary>
    private static async Task PublishSimpleAsync(IKnowledgeHubPageService pages, Guid pagePk, string html)
    {
        var edit = await pages.GetPageForEditAsync(pagePk);
        if (!edit.OkNotNull) return;

        edit.Value.ContentHtml = html;
        var saved = await pages.SaveDraftAsync(edit.Value);
        if (!saved.Ok) return;

        await pages.PublishAsync(pagePk, saved.Value);
    }

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
