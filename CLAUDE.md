# CLAUDE.md — MgSoftDev.KnowledgeHub

Contexto del proyecto para asistentes de código. Léelo al iniciar sesión.

## Qué es

**MgSoftDev.KnowledgeHub**: familia de paquetes NuGet que empaqueta el módulo de documentación
colaborativa validado en `../DocBookDemo` (DocsPortal, que queda INTACTO como referencia).
Multi-motor de BD (SQL Server, LiteDB; PostgreSQL futuro) y multi-hosting (WPF BlazorWebView,
Blazor Server, Blazor WASM). Estado: **10 fases completas y verificadas**, v0.1.0-preview.1.
Iteraciones posteriores: RCL embebible (v0.2.0-preview.1), **icono + color por página**
(v0.3.0-preview.1, cambio de esquema), **saneado de HTML + tool de tamaño de imagen**
(v0.4.0-preview.1, paquete nuevo → 10 paquetes), **limpieza de imágenes huérfanas + sincronía del
árbol** (v0.5.0-preview.1), **orden de páginas con invariante 1..N** (v0.6.0-preview.1) y
**3 niveles de limpieza de HTML** (v0.7.0/0.7.1-preview.1) y **títulos repetibles con slug
automático** (v0.8.0-preview.1).

## Arquitectura (decisiones clave)

- **Híbrido store/providers**: `IKnowledgeHubStore` (coarse-grained, en Abstractions) +
  sub-paquetes oficiales por motor. Un anfitrión puede implementar el store él mismo.
  Reglas de negocio SIEMPRE en los servicios core; el store solo persiste y aplica el filtro
  de visibilidad DENTRO de la query.
- **Sin tablas de usuarios**: el anfitrión implementa `IKnowledgeHubUserContext` (UserName,
  Permissions strings, catálogo). Permisos reservados: `KnowledgeHub.Admin/.Edit/.Publish/
  .ManagePermissions` (los 2 últimos solo con opt-in en `KnowledgeHubOptions`). Visibilidad
  por página = permisos arbitrarios del catálogo del anfitrión (`DocPagePermission.Permission`).
- **Auditoría**: `RowUserCreate/RowUserUpdate` (string username), sin FKs a usuarios.
- **IDs/timestamps en cliente**: `Guid.CreateVersion7()` + `DateTime.Now` vía `EntityStamp`
  (core). El DDL SQL conserva DEFAULTs solo como red de seguridad.
- **Vidas**: core y stores **Scoped** (WPF: scope raíz vive toda la app; Server: por circuito;
  WASM: ≈singleton). `LiteDatabase` e `IDbContextFactory` singleton.
- **Imágenes**: HTML almacenado usa `docimg://{pk}`; display = `{base}/{hash}.webp`. La regex
  de reversión al guardar es genérica por sufijo `{sha256hex}.webp` (funciona con virtual host
  WPF, endpoint relativo Server y URL absoluta WASM). Regex centralizadas en `KnowledgeHubHtml`.
  **Pipeline de ESCRITURA** (`KnowledgeHubImageService.UploadOrReplaceAsync`, único camino para
  editor/HTTP): reduce el ancho si supera `MaxImageWidth` (default 1600; **no hay tope de alto**),
  recomprime SIEMPRE a WebP y deduplica por SHA256 del binario ya convertido → se persiste el
  procesado, nunca el original. Si la imagen no se puede decodificar (SVG…), desde v0.3.1
  `SaveDraftAsync` **rechaza el guardado** con `Unfinished` en vez de dejar el base64 inline
  (ver gotcha 14). El caché de display y el rewriter NO tocan píxeles: solo leen.
- **Returning sobre HTTP**: `ApiResult<T>`/`ReturningTransport` (en Abstractions/Transport);
  HTTP 200 siempre que el pipeline funcionó, el conflicto de publicación viaja como Unfinished.
- **Rutas RCL**: prefijo fijo `/kh` (`KnowledgeHubRoutes`); CSS prefijado `kh-*`; la RCL no
  trae router (el anfitrión agrega `KnowledgeHubAssemblyMarker` a `AdditionalAssemblies`).
- **RCL en 3 capas (v0.2.0)** para poder embeber el módulo en apps con layout propio:
  1. `Components/Embedded/` — componentes atómicos SIN `@page` ni `@layout`
     (`KnowledgeHubNavTree`, `KnowledgeHubPageView`, `…PageEditor`, `…PageHistory`,
     `…VersionView`, `…PagePermissions`, `…PageManage`, `…SearchResults`, `…DiagnosticsPanel`).
  2. `KnowledgeHubBrowser` — compuesto maestro-detalle (árbol + panel) con navegación INTERNA;
     es el punto de integración de una línea para el anfitrión.
  3. `Components/Pages/` — páginas envoltorio de 2-3 líneas con las rutas `/kh/*`, **sin
     `@layout`** (adoptan el `DefaultLayout` del anfitrión). `KnowledgeHubLayout` se conserva
     para el escenario portal-standalone (lo fijan los 3 demos como `DefaultLayout`).
  - **Navegación con fallback**: cada componente expone `EventCallback` opcionales; si el
    anfitrión NO los pasa, el componente navega por URL (`KnowledgeHubRoutes`); si los pasa,
    delega. Patrón: `if (OnX.HasDelegate) await OnX.InvokeAsync(pk); else Nav.NavigateTo(...)`.
  - **Divisor arrastrable (v0.9.0)**: los dos shells (el layout portal y `KnowledgeHubBrowser`)
    duplicaban el mismo `grid-template-columns: 320px 1fr`. Ahora ambos delegan en
    `Components/Embedded/KnowledgeHubSplitLayout`, un `RadzenSplitter` de dos panes con el ancho
    recordado en localStorage. El plegado de un clic existe pero va **apagado por defecto**
    (`TreeCollapsible`): las flechas meten dos objetivos pulsables en una barra cuyo trabajo es que
    la arrastren, y darle sin querer hace desaparecer una columna. Defaults en `KnowledgeHubBlazorOptions`
    (`TreeSize`/`TreeMinSize`/`TreeMaxSize`/`TreeCollapsible`/`TreeWidthStorageKey`), overridables
    por instancia en el Browser. `.kh-portal` sobrevive como rejilla simple porque la guía la
    documenta para composición manual. Ver gotcha 23.
  - CSS: alturas por variables `--kh-portal-height` / `--kh-editor-height` (default `100vh`);
    `KnowledgeHubBrowser` usa `.kh-embedded` (100% del contenedor).
  - **`Options.HeaderActionsComponent` lo renderiza `KnowledgeHubNavTree`** (no el layout), que
    es el único componente presente en los tres modos → el gancho funciona siempre. Compone con
    el `FooterContent` del árbol (primero el del anfitrión, luego el gancho). `KnowledgeHubLayout`
    NO debe renderizarlo (sería doble). `KnowledgeHubBrowser` expone `TreeFooterContent` como
    passthrough al `FooterContent` del árbol.
- **Orden de páginas (v0.6.0)**: invariante **1..N por grupo de hermanos**, mantenida por el core
  (`NormalizeSiblingsAsync`) tras crear, mover, borrar y reordenar. La UI de Gestionar usa
  `MovePageOrderAsync(pk, Up|Down)` con botones y muestra «Posición N de M» — ya no se teclean
  índices. `NormalizeAllPageOrdersAsync` (solo Admin, botón en Diagnóstico) arregla bases antiguas.
  El store expone `SetSortOrdersAsync` (escritura en lote **atómica**) y `PageLinkDto` lleva
  `SortOrder` + `Title` para poder renumerar sin consultas extra. Ver gotcha 20.
- **Slug (v0.8.0)**: identificador **interno e invisible**. Las rutas van por `Guid`, no hay
  `GetPageBySlugAsync` en el store, no se muestra ni se edita en la UI; sus únicos consumidores son
  `KNOWLEDGEHUB_STARTPAGE` (solo DEBUG) y el `permissionsBySlug` del seeder. Por eso **crear una
  página nunca falla por el slug**: `KnowledgeHubSlug.Slugify(title)` (en Abstractions) lo deriva del
  título y `ResolveFreeSlugAsync` le añade `-2`, `-3`… si la base está ocupada. `CreatePageAsync`
  acepta `string? slug = null`; pasarlo solo fuerza la base, que recibe el mismo tratamiento.
  La unicidad **sigue siendo global** —es lo que imponen los índices únicos de los 3 proveedores—,
  así que **no hay migración de BD**. Ver gotcha 23.
- **Mantenimiento de imágenes (v0.5.0)**: `AnalyzeOrphanImagesAsync`/`DeleteOrphanImagesAsync` en
  `IKnowledgeHubImageService` (solo Admin), con UI en `KnowledgeHubDiagnosticsPanel` (analizar →
  confirmar → borrar). Huérfana = **no referenciada por ninguna versión** (gotcha 20). El borrado
  es FÍSICO (metadatos + binario + enlaces), no baja lógica: el objetivo es liberar espacio.
- **Sincronía de UI**: `KnowledgeHubUiState` (Scoped, en la RCL) es un bus mínimo de eventos;
  `PageManage` y `PageEditor` disparan `NotifyPageTreeChanged()` tras renombrar/mover/reordenar/
  icono/crear/eliminar/publicar y `KnowledgeHubNavTree` recarga. El anfitrión también puede
  dispararlo tras cambios hechos desde sus propias pantallas.
- **Selección de texto**: los temas de Radzen pisan `::selection` global con
  `--rz-primary-lighter` (12% de opacidad → casi invisible). `knowledgehub.css` la restaura **solo
  dentro de las superficies del módulo**, con las variables `--kh-selection-*`.
- **Editor tools**: `EditorToolDescriptor` en `KnowledgeHubBlazorOptions.EditorTools`; los
  built-in (4 callouts + `ImageSize` + `SanitizeHtml`) se registran por el mismo mecanismo
  (removibles). `EditorToolContext` da además `Editor` (para trabajar sobre la SELECCIÓN),
  `GetHtml()` y `ReplaceAllAsync()` (para herramientas de documento completo).
  **Primer JS de la RCL** (v0.4.0): `wwwroot/knowledgehub.js` con `imageNaturalSize`, cargado por
  *import* dinámico → el anfitrión no añade ningún `<script>`.
- **Saneado de HTML (v0.4.0)**: `IKnowledgeHubHtmlSanitizer` (Abstractions, SIN dependencias) +
  impl por defecto en el paquete aparte `MgSoftDev.KnowledgeHub.HtmlSanitizer`. Es **opcional**:
  se resuelve con `GetService<T>()` y si falta no se limpia nada (patrón de `IKnowledgeHubImageCache`).
  Tres puntos de llamada: pegar (`Paste` del editor), guardar (`SaveDraftAsync`, tras los dos
  rewrites de imagen y antes de `GetExistingImagePksAsync`) y el botón manual. Al guardar limpia y
  **loguea** (Information) sin molestar al usuario; el pase es idempotente. La allow-list DEBE
  incluir los esquemas `data` y `docimg` y el CSS `zoom` (gotcha 16).
- **Niveles de limpieza (v0.7.0)**: `HtmlCleanupLevel { Standard, Strict, PlainText }` y una
  sobrecarga `Sanitize(html, context, level)` con **implementación por defecto en la interfaz**
  (delega en la de dos argumentos) → los anfitriones que ya implementaban el contrato no se rompen.
  El paquete por defecto sostiene **una instancia de HtmlSanitizer por nivel** más un pre-proceso
  con AngleSharp (`HtmlPreProcessor`) obligatorio en Strict/PlainText (gotcha 22). El nivel es de
  **UI**: lo usan pegar y el botón manual; **guardar NO lo usa** a propósito, para que un nivel 3
  olvidado no pueda arrasar el formato de una página al guardarla. Vive en `KnowledgeHubUiState`
  (Scoped → dura la sesión), lo inicializa `KnowledgeHubBlazorOptions.DefaultCleanupLevel`, y se
  elige con 3 tools que se pintan como grupo de radio vía `EditorToolDescriptor.IsSelected`
  (`RadzenHtmlEditorCustomTool.Selected`); `IsVisible` las oculta —y a la escoba— si no hay
  sanitizador. Strict conserva lo que produce la propia librería: las `<img>` (tamaño/zoom) y los
  callouts, que desde esta versión se marcan con `class="kh-callout"` (los creados antes no la
  llevan → pierden el fondo si les pasas la escoba en nivel 2).
- **Icono + color por página (v0.3.0)**: propiedad ESTRUCTURAL del nodo (`DocPage.Icon`,
  `DocPage.IconColor`, NVARCHAR 64/32), no versionada. Se propaga por todos los DTOs donde
  aparece el título (`PageTreeNodeDto`, `PageInfoDto`, `PageReadDto`, `PageEditDto`,
  `SearchResultDto` + los de store `PageHeaderDto`/`SearchCandidateDto`). Se edita en
  `KnowledgeHubPageManage` con `KnowledgeHubIconPicker` (grilla Material Symbols temática +
  nombre manual + color) y su propio botón "Guardar icono" (`SetPageIconAsync`, molde
  `SetSortOrder`/`Rename`: store→service→HTTP `POST /pages/{pk}/icon`). Se pinta con el
  presentacional compartido `KnowledgeHubPageIcon` (`RadzenIcon` + color, fallback `article`)
  en árbol, lector, editor, búsqueda y el preview de gestión. Nullable → páginas viejas sin
  icono siguen igual; LiteDB sin migración (BSON), SQL con `ALTER ADD` idempotente (gotcha 12).

## Estructura

```
MgSoftDev.KnowledgeHub.slnx        (.NET 10, C# 14, CPM, TreatWarningsAsErrors)
Abstractions/    contratos+entidades+DTOs+Transport (dep: solo MgSoftDev.Returning)
KnowledgeHub/    servicios core + rewriter + FileSystemImageCache + seeder (dep: +ImageSharp)
Storage.LiteDb/  provider LiteDB (single-process, SemaphoreSlim+BeginTrans para atomicidad)
Storage.EntityFramework/  base EF neutral (ValueGeneratedNever, schema/prefijo)
Storage.SqlServer/        UseSqlServer + CreateSchema.sql embebido (UTF-8 BOM) + EnsureDatabaseObjectsAsync
Blazor/          RCL 11 componentes + editor tools + knowledgehub.css (dep: SOLO Abstractions)
AspNetCore/      MapKnowledgeHubAssets (immutable + cache-aside)
Http.Server/     MapKnowledgeHubApi (minimal API, auth del anfitrión vía configureGroup)
Http.Client/     impls HttpClient de los contratos (WASM-safe)
HtmlSanitizer/   impl por defecto de IKnowledgeHubHtmlSanitizer sobre Ganss.Xss (OPCIONAL, dep: +HtmlSanitizer 9.1.x-beta)
Demos/SharedAuth/     auth de demo compartida (users/roles LiteDB propio + AdminUsers/HostLinks/RootRedirect + SerilogReturningLoggerService)
Demos/Wpf/            anfitrión WPF+LiteDB (TFM net10.0-windows10.0.19041.0, virtual host docs-assets)
Demos/BlazorServer/   anfitrión Server+LiteDB (cookie auth, patrón AcceptsInteractiveRouting, puerto 5210)
Demos/Wasm(+.Server)/ anfitrión WASM hosted + API (token opaco en memoria, puerto 5220)
Tests/KnowledgeHub.ParityHarness/  guion de paridad: modos inmemory|litedb|sqlserver|http
.github/workflows/publish-nuget.yml  CI: publica a nuget.org al pushear tag v* (Trusted Publishing/OIDC)
artifacts/       feed NuGet local, git-ignored (dotnet pack -c Release -o artifacts)
```

## Publicación (nuget.org)

Los paquetes están **publicados en nuget.org** (perfil `migeru_garcia`), primera versión
`0.1.0-preview.1`; desde v0.4.0 son **10** (se sumó `HtmlSanitizer`). La publicación es automática:
`.github/workflows/publish-nuget.yml` se dispara
al pushear un tag `v*`, empaqueta los proyectos de librería (glob `MgSoftDev.KnowledgeHub*/*.csproj`
en ubuntu-latest — NO `dotnet pack` del `.slnx`, que arrastraría el demo WPF `net10.0-windows`
que no compila en Linux), toma la versión del tag (`-p:Version=${GITHUB_REF_NAME#v}`), y sube con
**Trusted Publishing (OIDC)** vía `NuGet/login@v1` (usuario `migeru_garcia`, sin API keys). Nueva
release = tag/versión nuevo (nuget.org no permite re-publicar una versión existente).

## Verificación (cómo se probó)

- **Guion de paridad** (99 checks; 7 de icono en v0.3.0, 3 de data-URI en v0.3.1, 6 de saneado en
  v0.4.0, 10 de huérfanas en v0.5.0, 11 de orden en v0.6.0, 15 de niveles de limpieza en v0.7.0/0.7.1
  y 7 de slug en v0.8.0
  —estos últimos llaman al sanitizador DIRECTAMENTE, porque los niveles son de UI): contra InMemory,
  LiteDB, SQL Server (`DEVSQL2022` o `(localdb)\MSSQLLocalDB`, BD temporal `KnowledgeHubParity`)
  y a través de HTTP (Kestrel real). `dotnet run --project Tests/KnowledgeHub.ParityHarness --
  <modo>`; sqlserver necesita `KH_SQLSERVER_CS` y BD vacía; http levanta Kestrel en
  127.0.0.1:5599. El ALTER-ADD del icono se verificó además creando una tabla `DocPages` v0.2
  vacía y confirmando la migración en caliente.
- **Demos**: WPF verificado por logs/publish; Server y WASM verificados en navegador
  (login, árboles por rol, assets immutable, normalización docimg:// al guardar desde WASM).
- **Humo NuGet**: app mínima en scratchpad restaurando SOLO desde `artifacts/` (9/9 PASS).

## Gotchas aprendidos AQUÍ (además de los 7 de ../DocBookDemo/CLAUDE.md)

1. **`return null;` desnudo en lambda con destino `Returning<T>`** produce una referencia
   `Returning` NULA (no pasa por la conversión implícita) → NRE en el caller. Siempre tipar:
   `return (MiDto?)null;` o ternario tipado. Los ternarios `x is null ? null : new(...)` sí
   son seguros. Cubierto por los checks 40-46 del guion de paridad.
2. **`ReturningEnums` vive en `MgSoftDev.ReturningCore.Helper`** (para implementar
   `IReturningLoggerService` hace falta ese using).
3. **API real de Returning**: `Returning.Success(value)` (genérico), `Returning.Unfinished(
   title, mensaje, notifyType)` devuelve `ReturningError` con conversión implícita a todos los
   tipos Returning. NO existen `ReturnValue`/`FromReturning`.
4. **Cookie login en Blazor Server .NET 10**: `[ExcludeFromInteractiveRouting]` requiere el
   patrón `HttpContext.AcceptsInteractiveRouting()` en App.razor (render mode condicional);
   con `@rendermode` fijo en `<Routes>` la página de login no resuelve.
5. **LiteDB Direct mode**: dos `LiteDatabase` no comparten archivo → el auth del demo va en
   `demo-auth.db` separado de `demo-knowledgehub.db`.
6. **net10.0 incluye System.Net.Http.Json y M.E.DependencyInjection** — referenciarlos como
   paquete dispara NU1510 (warning-as-error).
7. **Clicks sintéticos del Browser pane no disparan eventos Blazor WASM** (Server sí);
   verificar WASM con JS: `element.click()` y setters nativos + `dispatchEvent(new Event('input'))`.
8. **`nuget.config` con fuente local de ruta relativa (`<add value="artifacts" />`) rompe el
   restore en checkout limpio** (`NU1301`: la carpeta está git-ignored y solo la crea `dotnet
   pack`). Rompió el primer run del CI. El `nuget.config` del repo debe listar solo `nuget.org`
   (con `<clear/>`); el feed local va en el `nuget.config` del proyecto consumidor con ruta
   ABSOLUTA.
9. **Un `@layout` explícito en una página GANA sobre el `DefaultLayout` del Router del
   anfitrión** — por eso la RCL v0.1 no se podía embeber en apps con layout propio. Las páginas
   de la RCL ya no declaran `@layout`; quien quiera el shell del módulo debe fijarlo como
   `DefaultLayout` (así lo hacen los demos).
10. **`RadzenLink` no admite `@onclick` + `@onclick:preventDefault`** (RZ10010: el parámetro
    'onclick' quedaría duplicado). Para un link que a veces navega y a veces delega, renderiza
    condicionalmente `<a @onclick>` vs `<RadzenLink Path>` (ver `KnowledgeHubSearchResults`).
11. **El cuerpo async de `AsyncReturningCommand` NO resume en el Dispatcher de Blazor.** Invocar
    ahí un `EventCallback` del anfitrión (que provoca `StateHasChanged` en su componente) lanza
    *"The current thread is not associated with the Dispatcher"*. Solución: envolver en
    `await InvokeAsync(async () => { ... })` (inline si ya estás en el Dispatcher). Solo aplica
    dentro de comandos; los callbacks disparados desde handlers `@onclick` ya van en el
    Dispatcher. Caso real: `KnowledgeHubPageEditor.PublishCommand` → `OnPublished` (v0.2.0).
12. **Migración de columnas en SQL Server con DDL idempotente**: el `CREATE TABLE` va dentro de
    `IF OBJECT_ID(...) IS NULL BEGIN … END`, así que en instalaciones existentes ese bloque se
    SALTA y las columnas nuevas nunca se agregarían. La migración correcta es un `ALTER TABLE …
    ADD` guardado por `IF COL_LENGTH(N'[schema].[tabla]', N'Col') IS NULL`, colocado FUERA del
    `IF OBJECT_ID`. `EnsureDatabaseObjectsAsync` corre el script completo en cada arranque → migra
    en caliente sin tocar instalaciones nuevas (donde el CREATE ya trae la columna). Caso real:
    `Icon`/`IconColor` en `DocPages` (v0.3.0). Verificado creando una tabla v0.2 vacía y
    confirmando que el arnés añade las columnas y pasa 53/53.
13. **RadzenTreeLevel con icono por nodo**: para pintar algo antes del texto (icono + título) hay
    que reemplazar `TextProperty` por un `<Template Context="data">`; el `data` es un
    `RadzenTreeItem` y su `.Value` es `object?` → castear con `(PageTreeNodeDto)data.Value!`
    (si no, CS8600/CS8602 con TreatWarningsAsErrors). Caso real: `KnowledgeHubNavTree` (v0.3.0).
14. **Un `continue` que traga un `Returning` fallido esconde dos bugs distintos** (v0.3.1).
    `InterceptDataUrisAsync` hacía `if (!uploaded.OkNotNull) continue;`: (a) el data-URI se
    quedaba INLINE en el `ContentHtml` almacenado —base64 ≈ 4/3 del binario, duplicado en cada
    versión porque el versionado es insert-only— sin que el usuario se enterara, y (b) un error
    real de store/BD quedaba silenciado igual que un rechazo de negocio. Reglas: **distinguir
    siempre** `UnfinishedInfo is null` (Error de infra → `Throw()`) de un rechazo de negocio
    (reportar al llamador), y que el fallo de decodificación de ImageSharp se clasifique como
    `Unfinished` y no como excepción — para eso hay que capturar **`ImageFormatException`**, que
    es la base de `UnknownImageFormatException` e `InvalidImageContentException` (SVG = formato
    desconocido). El grupo `mime` de `DataUriRegex` existía sin usarse; ahora nombra el formato
    en el mensaje de rechazo.
15. **`RadzenHtmlEditorCustomTool.OnClick` NO llama a `SaveSelectionAsync()`** (el
    `RadzenHtmlEditorImage` de Radzen sí lo hace, por eso su diálogo funciona). Una tool custom que
    abra un diálogo pierde la selección —el diálogo roba el foco— y no puede reemplazar lo
    seleccionado. Solución: `await args.Editor.SaveSelectionAsync()` en `OnEditorExecute` **antes**
    de invocar la tool, y `RestoreSelectionAsync()` en la tool antes de devolver el HTML. Radzen
    marca la `<img>` clicada con `rz-state-selected` y la selecciona como rango, y
    `GetSelectionAttributes<T>("img", …)` la trata como caso especial. Caso real: `ImageSizeTool`.
16. **La allow-list de fábrica de HtmlSanitizer destruye contenido de KnowledgeHub** si no se
    amplía (verificado empíricamente contra 9.1.968-beta): tira `src="docimg://…"` y
    `src="data:…"` porque solo permite los esquemas `http`/`https`, y tira `zoom` porque no está
    entre sus 239 propiedades CSS (`width`/`height` sí lo están). Es decir: sin los tres ajustes de
    `KnowledgeHubSanitizerDefaults`, guardar **borraría todas las imágenes** de todas las páginas.
    Lo bueno: la basura de Word (`o:p`, `MsoNormal`, `v:shape`, comentarios condicionales), los
    `<script>` y los `on*` ya los quita de fábrica, y los callouts sobreviven (normaliza colores a
    `rgba()`, cosmético). Verificado también: es thread-safe (singleton OK) e idempotente — de eso
    depende que el log de "se saneó" no salte en cada guardado.
17. **HtmlSanitizer estable arrastra un AngleSharp vulnerable.** La 9.0.967 fija AngleSharp en
    `[0.17.1]` (rango EXACTO, no se puede subir), afectado por **CVE-2026-54570**: un fallo de
    parseo de MathML `annotation-xml` que permite **evadir sanitizadores** — justo lo que el
    paquete debe impedir. Además dispara NU1902 y, con `TreatWarningsAsErrors`, rompe el restore.
    La línea `9.1.x-beta` usa AngleSharp 1.5.2 (parcheado) y restaura limpio: por eso el repo usa
    una beta a propósito. Al actualizar, comprobar si ya hay estable con AngleSharp ≥ 1.5.0.
18. **En Blazor Server el editor choca con el límite de 32 KB de SignalR** (`MaximumReceiveMessageSize`,
    default de fábrica). El `RadzenHtmlEditor` manda el documento ENTERO por el circuito, tanto al
    pegar (evento `Paste` → `invokeMethodAsync('OnPaste', html)`) como al cambiar el valor enlazado.
    Al superarlo, SignalR **cierra la conexión y el pegado se pierde sin ningún error**: el JS de
    Radzen envuelve la llamada en `try{}catch{}` y la cadena `.then(html => insertHTML)` nunca
    corre. Síntoma: "pego de Word y no pasa nada, ni una palabra". **Solo afecta a Server** — WPF
    (BlazorWebView) y WASM no tienen salto por SignalR, por eso ahí funciona igual. Los demos y la
    guía (§6) lo suben a 10 MB. Reproducido midiendo: 301 B pega bien, 51 KB no pega nada; con el
    límite subido, el mismo payload pega y se limpia.
    Corolario de diseño: **enlazar `Paste` NO es gratis**. Radzen solo dispara el evento si el
    callback tiene delegado, y enlazarlo cambia el pegado nativo del navegador por un round-trip
    JS→.NET. Por eso `KnowledgeHubPageEditor` lo engancha **solo si hay un `IKnowledgeHubHtmlSanitizer`
    registrado** (`OnInitialized` → `EventCallback.Factory.Create`); si no, deja el pegado nativo
    intacto y los hosts sin sanitizador no pagan nada.
19. **Las proyecciones `Query().Select(...)` de LiteDB PIERDEN el Pk en silencio.** LiteDB guarda
    el id mapeado como `_id`, así que `Select(i => new { i.Pk, ... })` deserializa `Pk` como
    `Guid.Empty` (y `Select(v => v.ContentHtml)` a un string pelado no devuelve nada), **sin lanzar
    ninguna excepción**: el `Returning` viene Ok y los datos silenciosamente mal. Detectado porque
    la limpieza de huérfanas daba `Ref=0/Orphan=6` en LiteDB y `Ref=5/Orphan=1` en InMemory con los
    mismos datos, y borraba 0. Regla: en LiteDB usar `FindAll()`/`Find(...)` y proyectar en memoria
    con LINQ-to-objects. `FindAll()` va documento a documento, así que no hay que temer a la
    memoria. Esta clase de bug NO lo detecta el compilador — solo el arnés en varios proveedores.
20. **El orden de páginas se mantenía a mano y se degradaba solo** (arreglado en v0.6.0). Tres
    fallos sumados: `MovePageAsync` **no reasignaba** `SortOrder` (la página aterrizaba en el padre
    nuevo con el orden del anterior), `DeletePageAsync` **no renumeraba** a los hermanos restantes,
    y `GetMaxSortOrderAsync` **no filtraba `RowIsActive`** en los 3 proveedores, así que el `MAX+1`
    de `CreatePageAsync` contaba páginas borradas. Resultado real: la 5ª hermana con `SortOrder` 15
    y una UI que te pedía teclear el índice a mano. Ahora hay una **invariante**: cada grupo de
    hermanos está siempre en 1..N, garantizada por `NormalizeSiblingsAsync` tras crear/mover/
    borrar/reordenar. El renumerado usa el mismo criterio que `BuildTree`
    (`OrderBy(SortOrder).ThenBy(Title)`) para no reordenar lo que el usuario está viendo.
21. **El vínculo página↔imagen NO sirve para saber si una imagen se usa.** `ReplacePageImageLinks`
    deja en `DocPages_DocImages` únicamente las imágenes de la **última versión guardada** de cada
    página, así que una imagen usada solo en una versión antigua ya aparece sin enlaces. Calcular
    "huérfanas" desde esa tabla borraría imágenes del historial y rompería `RestoreVersionAsync`.
    Lo correcto es `GetReferencedImagePksAsync`, que escanea el `ContentHtml` de TODAS las versiones
    con `KnowledgeHubHtml.ExtractDocImagePks`. Cubierto por el check "Imagen usada solo en el
    historial NO se borró".
22. **Endurecer HtmlSanitizer tiene cuatro trampas, todas verificadas ejecutándolo** (v0.7.0):
    (a) **`KeepChildNodes` (default `false`) BORRA el texto** del tag que quitas — sacar `span` de
    la allow-list convierte `<h2>Titulo <span>interno</span></h2>` en `<h2>Titulo </h2>`; hay que
    ponerlo en `true` en cuanto se quite un tag de formato. (b) **Con `KeepChildNodes=true` se
    filtra el TEXTO de `<script>`/`<style>`** al resultado (`okalert(1)p{color:red}`); no es XSS
    (va escapado) pero es basura visible → hay que eliminar esos nodos ANTES de sanear.
    (c) **Aplanar a `AllowedTags` pega los textos sin separación**: con `{img}` la salida real es
    `TituloParrafo unoParrafo dosab`, y **`{img,p,br}` no basta** porque `h2`/`li`/`div` siguen
    colapsando contra sus vecinos → hay que convertir los bloques a `<p>` en el pre-proceso.
    `PostProcessNode`/`PostProcessDom` **NO sirven** para (b) y (c): corren DESPUÉS del aplanado,
    cuando esos nodos ya no existen; por eso el pre-proceso es un paso propio con AngleSharp
    (`HtmlPreProcessor`), añadido como `PackageReference` explícito a la 1.5.2 que ya resolvía
    como transitiva. (d) **El shorthand `background` deja residuos**: AngleSharp lo expande a
    longhands, así que quitar `background`/`background-color` de `AllowedCssProperties` deja
    `background-position: initial; background-size: initial; …` → hay que quitar **todos** los
    `background-*`. Para preservar lo propio (imágenes y callouts) el evento **`RemovingStyle`**,
    que se dispara por CADA propiedad y admite `Cancel`, es el gancho correcto; `AllowedClasses`
    filtra clase a clase, así que `class` puede permitirse dejando solo `kh-callout`.
    **Corolario (v0.7.1, tras un bug real):** las trampas (c) y (d) son síntomas de lo mismo —
    quitar de las listas es una estrategia que **falla abierta**. El primer intento listaba las
    propiedades a quitar y un pegado real de una web coló `orphans: 4` entero, porque a nadie se le
    ocurre listar `orphans`. Y en PlainText **vaciar `AllowedTags` no basta**: `style` seguía en
    `AllowedAttributes` con las 239 propiedades de fábrica, así que el `<p>` conservaba colores y
    fuentes y el nivel 3 era idéntico al 1. Ahora ambos niveles **sustituyen** las listas
    (`AllowedCssProperties`, `AllowedAttributes`, `AllowedTags`) por las suyas y fallan cerrados.
    Regla para tests: comprobar qué **etiquetas** sobreviven no dice nada de los **atributos** —
    hay que afirmar la salida exacta.
    Trampa (e), del mismo arreglo: renombrar a `<p>` un bloque que YA contiene párrafos produce
    `<p><p>x</p></p>`, que el parser parte en **dos `<p>` vacíos** alrededor del bueno (líneas en
    blanco visibles). Si el bloque contiene otro bloque hay que **desenvolverlo** (fragmento), no
    renombrarlo — y el selector de «¿contiene bloques?» debe incluir `p`.

23. **Un identificador invisible estaba bloqueando el trabajo real** (arreglado en v0.8.0). El slug
    tenía unicidad GLOBAL validada en `CreatePageAsync`, así que **no se podían tener dos páginas con
    el mismo título aunque colgaran de padres distintos** — imposible documentar dos aplicaciones en
    un mismo árbol, cada una con su "Empezar". Y como `SlugExistsAsync` **no filtra `RowIsActive`**
    (a propósito: el índice único de la BD tampoco distingue), un título usado y borrado quedaba
    **reservado para siempre**, sin forma de liberarlo por UI ni API. Todo eso por un campo que
    **no se usa para nada**: rutas por `Guid`, sin lookup por slug, invisible y no editable.
    Antes de endurecer una restricción de unicidad, comprobar **quién consume realmente el campo**.
    La solución barata fue dejar la unicidad global (cero migración en las BD existentes) y
    **desambiguar en el servicio** con sufijos `-2`, `-3`. La cara habría sido unicidad por hermanos:
    migrar el índice único en SQL Server, LiteDB y EF, **y** añadir validación a `MovePageAsync`
    (hoy no valida nada), es decir, cambiar un error por otro error nuevo.
    Cuidado con `$"{prefijo}-{Guid.NewGuid():n}"[..N]`: el `[..N]` trunca **la cadena entera**, no el
    guid — el código viejo del árbol lo tenía y generaba slugs cortados.
24. **Lo que hay que saber del `RadzenSplitter` 11.1.5** (v0.9.0, todo verificado leyendo el
    paquete y midiendo en el demo):
    - `.rz-splitter` ya es `width/height: 100%`, así que basta con que el contenedor tenga altura;
      no hace falta `Style="height:100%"`. Pero `.rz-splitter-pane` es **`overflow:hidden`**, así
      que lo que metas dentro necesita `height:100%` para que su propio `overflow:auto` aparezca.
    - El pane es `position:relative`. **No** rompe el `position:absolute; inset:0` del
      `RadzenHtmlEditor`, porque el ancestro posicionado más cercano sigue siendo `.kh-editor-host`
      (comprobado midiendo `offsetParent` en el navegador).
    - El drag escucha **`pointermove`/`pointerup`**, no `mousemove`/`mouseup`: un arrastre simulado
      con `MouseEvent` no hace absolutamente nada.
    - **`Collapsible="false"` NO quita las flechas de la barra**: deja de funcionar el plegado, pero
      Radzen sigue pintando un `<span class="rz-expand">` visible, focusable y de 16px. Para tener
      una barra limpia hay que ocultarlas por CSS (`.kh-split-nocollapse`).
    - `RadzenSplitterResizeEventArgs.NewSize` es `parseFloat(pane.style.flexBasis)`, es decir un
      **porcentaje**, no píxeles. Por eso el ancho se persiste midiendo el DOM
      (`getBoundingClientRect`) en vez de usar ese valor.
    - Cambiar `Size` en caliente no reposiciona el pane; para restaurar un ancho guardado hay que
      **remontar** el splitter (`@key` sobre el tamaño), que así nace con el valor correcto.
    - El primer pane es `flex: 0 0 auto` y **no encoge**: un ancho guardado mayor que el contenedor
      deja el panel de contenido en 0px sin forma obvia de recuperarlo. Pasó de verdad al restaurar
      520px en un contenedor de 384px. Se cierra por dos lados: recortando el valor guardado al
      contenedor al restaurar (`fitToContainer` en el JS) y con un tope CSS
      `--kh-tree-max-width: 75%` para cuando se estrecha la ventana después.

## Pendientes / siguientes pasos

- Captura visual del demo WPF (la sesión de Windows estaba bloqueada durante la verificación;
  el arranque/seed/auto-login se verificó por log). Env vars DEBUG: `KNOWLEDGEHUB_AUTOLOGIN=admin`,
  `KNOWLEDGEHUB_STARTPAGE=<slug|/ruta>`.
- Publicar versión estable `v0.1.0` (sin `-preview`) cuando la API se considere congelada:
  `git tag v0.1.0 && git push origin v0.1.0` (el resto es automático).
- Posible: provider PostgreSQL sobre Storage.EntityFramework; ContractTests xunit permanentes;
  persistir token WASM en sessionStorage; workflow de CI de build/test en PRs (hoy solo hay
  publish en tags).

## Repo / release

- Repo git en `Source\`, remoto `origin` = **https://github.com/MgSoftDev/KnowledgeHub** (público),
  rama default `main`.
- Publicado en nuget.org en `0.1.0-preview.1` (ver sección Publicación arriba).
