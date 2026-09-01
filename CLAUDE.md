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
  - **El árbol recuerda lo que cierras (v0.18.0)**: `KnowledgeHubUiState.CollapsedPages` (Scoped) +
    persistencia en localStorage bajo `Options.TreeExpansionStorageKey`. Se guardan las ramas
    **CERRADAS**, no las abiertas: así el defecto histórico (todo abierto) se mantiene, una página
    nueva nace abierta como sus hermanas y lo almacenado crece con lo que el usuario cambia, no con
    el tamaño del árbol. El estado **no puede vivir en el componente**: el `@key` del splitter lo
    remonta en el primer render cuando hay ancho guardado, y cada acción de gestión lo recarga.
    `CurrentPagePk` (o la URL, en modo enrutado) abre la cadena de ancestros de la página abierta y
    la marca con `RadzenTreeLevel.Selected`, sin tocar el resto. Ver gotcha 32.
  - **Índice «En esta página» (v0.20.0)**: columna pegajosa a la derecha del lector con los
    encabezados de la página; al pulsar salta, y el que estás leyendo queda marcado. Las anclas
    **se generan al renderizar y NO se persisten** — no podrían: el saneador borra `id` en los tres
    niveles y cada guardado sanea (gotcha 36). De ahí sale gratis lo que pedía el encargo: funciona
    en páginas escritas hace meses, **sin migrar ni tocar contenido**. La extracción es una función
    PURA en Abstractions (`KnowledgeHubHtml.BuildOutline`, regex `[GeneratedRegex]`), no un parseo
    en JS: la RCL solo referencia Abstractions, y siendo pura la cubre el arnés — bUnit no ejecuta
    JS. Corre **después** del rewriter de imágenes, así que un encabezado que produce un `{{ for }}`
    de Scriban se indexa como cualquier otro. Opciones: `ShowOutline`, `OutlineMaxLevel` (3) y
    `OutlineStorageKey`, las dos primeras overridables por instancia en el lector y en el Browser;
    el plegado vive en `KnowledgeHubUiState` (**no** como parámetro: el lector recarga del store en
    cada set de parámetros, así que sería un viaje a la BD por clic).
  - **Menú del árbol y enlaces entre páginas (v0.21.0)**: clic derecho sobre un nodo
    (`RadzenTree.ItemContextMenu` + `ContextMenuService`) con **Copiar ruta** / **Copiar enlace** y,
    con permiso de edición, Nueva página / Editar / Gestionar. Existe por lo primero: enlazar una
    página exige su Guid, que nadie va a teclear.
    Y lo segundo, que es lo que lo hace útil de verdad: **el lector intercepta los clics sobre esos
    enlaces** (`interceptPageLinks` en JS → `[JSInvokable] OpenPageFromLinkAsync` → `OnPageRequested`)
    y los resuelve con la navegación del módulo. Sin eso, un `/kh/page/{pk}` pegado funciona en el
    portal pero **en embebido saca al usuario de la pantalla del anfitrión** — a una ruta que puede
    ni estar mapeada. Quién decide si un href es nuestro: `KnowledgeHubRoutes.TryGetPagePk`, función
    pura y por tanto cubierta por el arnés; el mismo-origen lo decide el JS, que es quien ve la URL.
    No abre ninguna puerta: la página destino se carga por `GetPageForReadAsync` como cualquier otra.
    Opciones: `TreeContextMenu` (global) + `ShowContextMenu` por instancia — la salida para un
    anfitrión sin `<RadzenComponents />`. Ver gotcha 37.
  - CSS: alturas por variables `--kh-portal-height` / `--kh-editor-height` (default `100vh`);
    `KnowledgeHubBrowser` usa `.kh-embedded` (100% del contenedor).
  - **Pantalla de bienvenida sustituible (v0.10.0)**: `Options.HomeComponent` (`Type?`, mismo patrón
    que `HeaderActionsComponent`) reemplaza el texto de `/kh` y el estado vacío del Browser, donde
    el `EmptyContent` de la instancia sigue mandando. Hacía falta un gancho porque el anfitrión
    **no puede** declarar su propio `@page "/kh"`: el Router falla al arrancar con *"The following
    routes are ambiguous"*.
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
- **Clases del anfitrión (v0.13.0)**: `KnowledgeHubSanitizerOptions` con `AllowedClasses` y
  `AllowedClassPrefixes`, que llegan a los **niveles 1 y 2** — antes el `Action<HtmlSanitizer>` solo
  alcanzaba el 1, así que una maquetación propia sobrevivía al guardado pero moría con la escoba en
  nivel 2. Los prefijos existen porque enumerar una familia que crece es una lista que alguien
  olvidará, y la clase no registrada es indistinguible de basura. El nivel 3 las quita igualmente
  (es «solo texto»). Se pasa el **objeto**, no una lambda: un segundo `Action<…>` haría ambigua
  (CS0121) toda llamada existente, y compartir el mismo objeto entre contenedores es lo que evita
  la gotcha 28.
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
- **Exportación a PDF (v0.11.0, motor cambiado en v0.12.0)**: **dos** contratos, no uno.
  `IKnowledgeHubPdfExportService` (core, Scoped) es donde vive TODA la seguridad —`CanExport` una
  vez, `GetTreeAsync` para la rama y luego `GetPageForReadAsync` **página por página** como defensa
  en profundidad, saltando las rechazadas sin tumbar la exportación—; `IKnowledgeHubPdfRenderer`
  (opcional, paquete `MgSoftDev.KnowledgeHub.Pdf`) solo convierte a bytes. Esa separación se puso a
  prueba de verdad en la v0.12.0: **se cambió el motor entero de PDFsharp a Playwright sin tocar ni
  un contrato, ni el endpoint, ni la UI, ni el permiso**.
  Las imágenes viajan en un diccionario aparte y **como se almacenan (WebP)**, con los `docimg://`
  intactos en el HTML: sin inflar la cadena y dejando que cada motor decida. `MaxExportPages`
  (200) **rechaza**, nunca trunca. El permiso `KnowledgeHub.Export` con `UseFineGrainedExport` cae
  en `IsAuthenticated` —no en `CanEdit`— porque leer y exportar son la misma capacidad.
- **Qué páginas entran en el PDF (v0.14.0)**: además de publicada + visible, se descartan las
  marcadas con `DocPage.ExcludeFromPdf` (casilla en Gestionar; estructural, molde de `Icon`) y las
  **visualmente vacías** (`KnowledgeHubHtml.IsVisuallyEmpty`). La marca filtra en **`Collect`**, no
  en el bucle de lectura, porque `MaxExportPages` se mide sobre `planned`: filtrando después, una
  página excluida seguiría gastando cupo. Las vacías **no pueden** filtrarse ahí (su contenido no se
  conoce hasta leerlas) y sí gastan cupo; asumido para no pagar una segunda ida al store. Excluir
  una página **no** excluye su rama —el tablero se va, el manual que cuelga de él se queda—, y por
  eso hay un rechazo explícito para `includeDescendants: false` sobre una página marcada: sin él, el
  mensaje habría culpado a que no está publicada. Efecto conocido: una hija cuyo padre quedó fuera
  conserva su `Level` real, así que en el índice sale sangrada como si el padre siguiera ahí (ya
  pasaba con padres sin publicar).
- **Motor de PDF = Chromium vía Playwright (v0.12.0)**. PDFsharp se retiró: mantener a mano un
  mapeador HTML→documento no cubría la variedad real de la documentación, y sobre todo **hacía
  imposible el objetivo de temas por empresa** — un tema es CSS y solo un navegador aplica CSS.
  El navegador sale de `PdfBrowserSource` (`Auto` → Edge instalado → copia empaquetada → caché), y
  **no queda residente**: se abre y se cierra en cada exportación, serializado con un semáforo y con
  tope de tiempo, porque exportar es ocasional y una app de escritorio no debe sostener 200-300 MB.
  Portada, cabecera y pie los define el anfitrión con `PdfTemplate` (Html / FilePath leído en cada
  exportación / Factory) y hay `Css`/`AdditionalCss`/`CssFilePath` para el tema. El paquete ships un
  `.props` que fija `PlaywrightPlatform` al SO actual: sin él cada app consumidora se llevaría
  **548 MB** de driver a su salida en vez de 87.
- **Datos vivos en las páginas (v0.19.0)**: paquete opcional `MgSoftDev.KnowledgeHub.Templating`
  (Scriban **7.2.6**; BSD-2 y **cero dependencias** en net8.0+). **Dos contratos**, como el PDF:
  `IKnowledgeHubTemplateRenderer` (motor: `Validate` sin ejecutar + `RenderAsync`) e
  `IKnowledgeHubTemplateModelProvider` (los datos del anfitrión; se registran VARIOS, cada uno aporta
  una variable raíz). Modelo `kh` de fábrica: `kh.roles` sale del catálogo que el anfitrión **ya**
  pasa (`GetPermissionCatalogAsync`), más `kh.user.has`, `kh.page` y **`kh.is_pdf`**.
  **Se renderiza en `GetPageForReadAsync`** (no en `ToReadDto`, para que el **historial NO renderice**:
  es auditoría, y con datos de hoy dos versiones distintas se verían iguales). De ahí sale gratis el
  PDF y el WASM (el cliente es un proxy: el servidor renderiza antes de serializar, y Scriban no se
  compila al navegador). El **editor** está a salvo por construcción: usa `GetPageForEditAsync`, que
  arma otro DTO. **Nunca enganchar en el rewriter de imágenes** — lo comparte el editor.
  **Opt-in por página** (`DocPage.UsesTemplates`, molde de `ExcludeFromPdf`) porque las llaves son
  contenido normal en cualquier página que documente Angular o Handlebars, y **permiso propio**
  `KnowledgeHub.Templates`, el único **sin modo grueso**: recorre datos del anfitrión y ejecuta
  bucles en el servidor en cada visita. Scriban **no escapa nada**, así que la salida renderizada se
  sanea con `HtmlSanitizeContext.Render`. Publicar una plantilla rota **rechaza**; guardar el
  borrador solo **avisa** (un borrador no lo ve nadie, y bloquearlo deja al autor a medio `{{ for }}`).
  Ver gotcha 33.
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
Templating/      motor de datos vivos sobre Scriban 7.2.6 (OPCIONAL, cero deps transitivas)
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

- **Guion de paridad** (218 checks; 7 de icono en v0.3.0, 3 de data-URI en v0.3.1, 6 de saneado en
  v0.4.0, 10 de huérfanas en v0.5.0, 11 de orden en v0.6.0, 15 de niveles de limpieza en v0.7.0/0.7.1,
  7 de slug en v0.8.0, 14 de exportación a PDF en v0.11.0/0.12.0, 10 de clases del anfitrión en
  v0.13.0, 12 de páginas excluidas/vacías en v0.14.0, 8 de fugas por Guid en v0.15.0, 16 de creación
  visible + escalada cerrada en v0.16.0, 9 de borrado en cascada en v0.17.0 18 de datos vivos en
  v0.19.0, 13 del índice de la página en v0.20.0 y 7 de enlaces entre páginas en v0.21.0
  —los de limpieza llaman al sanitizador DIRECTAMENTE, porque los niveles son de UI): contra InMemory,
  LiteDB, SQL Server (`DEVSQL2022` o `(localdb)\MSSQLLocalDB`, BD temporal `KnowledgeHubParity`)
  y a través de HTTP (Kestrel real). `dotnet run --project Tests/KnowledgeHub.ParityHarness --
  <modo>`; sqlserver necesita `KH_SQLSERVER_CS` y BD vacía; http levanta Kestrel en
  127.0.0.1:5599. El ALTER-ADD del icono se verificó además creando una tabla `DocPages` v0.2
  vacía y confirmando la migración en caliente.
- **Pruebas de componentes** (36, bUnit, `Tests/KnowledgeHub.ComponentTests`): 13 de humo —cada
  componente embebible se monta una vez—, 4 de flujo del `KnowledgeHubBrowser`, 6 de la marca de
  selección del árbol en los dos modos, 7 del panel «En esta página» y 6 del menú contextual y los
  enlaces entre páginas. Desde la v0.21.0 el arnés puede montar `<RadzenComponents />`
  (`RenderRadzenOverlays`): sin ese host `ContextMenuService` no abre nada **ni lanza**, así que un
  menú roto pasaría por verde. Cubren lo que ni el compilador ni el arnés ven: **un
  componente que revienta al renderizar** (el comentario Razor dentro de la lista de atributos,
  gotcha 34) y **un flujo que acaba en la pantalla equivocada** (el eco del árbol, gotcha 35). Los servicios son **falsos**: si el core devuelve mal un DTO, eso no se
  ve aquí — es trabajo del arnés de paridad, y por eso el fallo de la casilla de la v0.19.1 pasó
  desapercibido en la UI y lo cazó el arnés.
  **Los dos tests se validaron reintroduciendo cada bug y comprobando que se ponen rojos**; unos
  tests que no fallan nunca dan la misma tranquilidad que no tenerlos.
- **CI** (`.github/workflows/ci.yml`, en cada push y PR a `main`): compila las librerías **una a una
  con un glob**, nunca el `.slnx` —incluye el demo WPF `net10.0-windows`, que no compila en Linux—,
  corre el arnés en `inmemory`, `litedb` y `http` (los tres se bastan solos; `sqlserver` queda fuera
  por necesitar instancia) y las pruebas de componentes. Gratis: el repo es público.
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
    filtra clase a clase, así que `class` puede permitirse dejando solo `kh-callout` — **con la
    trampa de que un `AllowedClasses` VACÍO permite TODAS** (su doc: *"If the set is empty, all
    classes will be allowed"*), así que sembrarlo es lo que ACTIVA el filtro y nunca hay que
    vaciarlo. Desde v0.13.0 el anfitrión suma ahí las suyas, y los prefijos van por el evento
    **`RemovingCssClass`** (su EventArgs hereda de `CancelEventArgs`, así que tiene `Cancel`).
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

25. **PDFsharp/MigraDoc 6.2.4** — *nota histórica: el paquete dejó de usarlo en la v0.12.0.* Se
    conserva por si alguien vuelve a plantear un motor sin navegador; en ese caso, estas tres se
    dan por seguras porque están medidas.
    - **Acepta WebP y genera el PDF SIN la imagen.** Ni excepción ni aviso. Verificado inspeccionando
      la estructura: con PNG sale `/Subtype /Image /Width 200 /Height 100`; el mismo documento con
      WebP sale con **cero** XObjects de imagen. Como KnowledgeHub almacena WebP, transcodificar con
      ImageSharp NO es opcional, y si falla hay que pintar un marcador visible — nunca callar.
    - **`PageSetup.PageFormat = A4` NO rellena `PageWidth`**: se queda en 0. Cualquier cuenta que
      reste los márgenes da **negativo** (−124,7 pt con márgenes de 2,2 cm) y la tabla sale con las
      columnas cambiadas y fuera de la página. Hay que fijar `PageWidth`/`PageHeight` a mano.
    - **`GlobalFontSettings` es estático de proceso.** Sin resolver, el render **lanza** (bien: falla
      ruidoso, no glifos vacíos). En Windows basta `UseWindowsFontsUnderWindows = true`; el resolver
      solo se asigna si está a null para no pisar al anfitrión. Los emoji salen como cuadrito si la
      fuente configurada no los tiene (Arial no).
    Extra de MigraDoc que Chromium no da: índice con `AddPageRefField` (números de página reales) y
    marcadores desde `ParagraphFormat.OutlineLevel`.

26. **Empaquetar Chromium con la app (Playwright) tiene cuatro trampas medidas** — ejemplo vivo en
    `Demos/KnowledgeHub.Demo.Wpf` (`Pdf/PlaywrightPdfRenderer.cs` + el MSBuild del csproj), apagado
    tras `KhBundleChromium`, que hay que encender con `-p:KhBundleChromium=true`.
    - **`PLAYWRIGHT_BROWSERS_PATH=0` al instalar NO basta**: en ejecución Playwright sigue mirando
      `%LOCALAPPDATA%\ms-playwright` y falla con «Executable doesn't exist at …». La app tiene que
      fijar la variable en su propio proceso antes de lanzar el navegador (no hay que tocar el
      equipo del cliente).
    - **Sin `<PlaywrightPlatform>win</PlaywrightPlatform>` se copia el driver de Node de las CINCO
      plataformas**: 548 MB en vez de 87 MB. Publish medido: **888,3 → 427,6 MB**.
    - **`dotnet publish` NO arrastra el navegador desde `bin`**: se descargó después de que MSBuild
      resolviera qué copiar, así que no lo conoce como item. Hace falta un segundo target en
      `AfterTargets="Publish"` que lo copie (o volvería a descargar 370 MB). Y ojo: `$(PublishDir)`
      está definido también en un build normal, así que no sirve para distinguir el enganche —
      por eso son dos targets que pasan la carpeta explícitamente.
    - **Playwright añade 119 caracteres de ruta** hasta el ejecutable. Pasado MAX_PATH (260) falla
      con `spawn … ENOENT`, que no menciona longitudes en ningún momento.
    Confirmado además: `PdfAsync` **sí** funciona con `Channel="chromium-headless-shell"` (265 MB
    frente a 412 MB del Chromium completo), y cachear el navegador baja la segunda exportación a
    396 ms.

27. **Imprimir con Chromium: tres trampas medidas** (v0.12.0).
    - **`PagePdfOptions.Outline` no hace NADA sin `Tagged`.** Chromium construye el esquema a partir
      de la estructura etiquetada, así que pedir solo marcadores devuelve el PDF sin ellos **y sin
      ningún error**. Medido con el mismo documento: `outline` solo → 41.934 bytes y cero
      marcadores; `outline + tagged` → 49.727 bytes y los títulos dentro; `outline=false` a secas →
      **byte a byte idéntico** al primero. Por eso `EmbedOutline` fuerza `Tagged`. Coste: ~19%.
    - **La cabecera y el pie viven en un contexto AISLADO**: no les llega el CSS del documento, no
      cargan imágenes por URL (hay que pasarlas como data URI) y los estilos deben ir en línea.
      Además, si el margen es menor que la cabecera, esta **se superpone al contenido** —comprobado
      con 5 mm: el logo pisa el primer encabezado— sin dar aviso. Deja ~22 mm si lleva imagen.
    - **`PLAYWRIGHT_BROWSERS_PATH=0` al instalar NO basta**: en ejecución Playwright vuelve a mirar
      `%LOCALAPPDATA%\ms-playwright`. La variable hay que fijarla también **en el proceso de la
      aplicación** antes de lanzar el navegador (así el equipo del cliente no configura nada).
    Y lo que SÍ sale gratis: el índice con anclas `#id` produce enlaces reales del PDF (`/Annots`),
    el WebP se incrusta sin transcodificar, y `Channel="msedge"` usa el Edge instalado sin descargar.

28. **El mismo documento lo limpian DOS sanitizadores distintos, y nada avisa cuando divergen**
    (v0.13.0, a partir de un caso real). Síntoma reportado: pegar HTML con clases propias en la
    vista código → se ve bien; darle a la escoba → no quita nada; **guardar → las clases desaparecen**.
    La escoba resuelve el sanitizador del contenedor de la **UI**
    (`BuiltInEditorTools.cs`, `ctx.Services.GetService<IKnowledgeHubHtmlSanitizer>()`) y el guardado
    usa el inyectado en el **core** (`KnowledgeHubPageService.SanitizeForSave`). Las reglas son las
    mismas —los dos van a nivel Standard—, así que **si el resultado difiere es que son instancias
    distintas**. En WASM son dos procesos: se había configurado el cliente y no el servidor de la
    API, que es quien decide lo que se almacena. Regla de diagnóstico: cuando pegar/limpiar y
    guardar no coinciden, no busques en las reglas, busca en el registro. La pista está en el log
    del servidor (`HTML saneado al guardar`, con los caracteres antes → después).
    Dos agravantes de diseño, ya cerrados: la sobrecarga `Action<HtmlSanitizer>` **solo alcanzaba el
    nivel 1** (los niveles 2 y 3 se construían dentro de `DefaultKnowledgeHubHtmlSanitizer` sin nada
    del anfitrión), y `AddKnowledgeHubHtmlSanitizer` usa **`TryAddSingleton`**, así que una llamada
    sin configurar hecha antes anula la configurada **sin decir nada**. La sobrecarga nueva recibe el
    **objeto** `KnowledgeHubSanitizerOptions` y no una lambda a propósito: un segundo `Action<…>`
    volvería ambigua (CS0121) toda llamada existente, y pasar el MISMO objeto a los dos contenedores
    es justo el hábito que impide que se separen.

29. **«Página vacía» no se puede decidir mirando la cadena** (v0.14.0). Al filtrar del PDF las
    páginas sin escribir aparecen dos trampas opuestas, las dos reales en este repo:
    (a) **`<p><br></p>` NO es un marcador de vacío.** `CalloutHtml.Build` lo añade a propósito al
    final de **cada callout** —para que Enter saque el cursor de la caja— y la guía se lo recomienda
    al anfitrión. Un `html.Contains("<p><br></p>")` habría borrado del PDF cualquier página cuyo
    contenido fuera un solo aviso. La pregunta correcta es «¿queda texto al quitar las etiquetas?».
    (b) **Una página de solo imagen no tiene texto y no está vacía**: `HtmlTagRegex().Replace(...)`
    la deja en cadena vacía. Por eso `IsVisuallyEmpty` mira primero `docimg://` y los elementos que
    se ven sin aportar texto (`img`, `iframe`, `video`, `embed`, `object`, `svg`, `canvas`).
    Y (c) hay que **decodificar entidades** antes de juzgar: los niveles 2 y 3 de limpieza pueden
    dejar un `<p>` con solo `&nbsp;`, que a una comparación cruda le parece texto.

30. **El store solo filtra en 3 métodos; un `versionPk` salta el filtro por diseño** (v0.15.0, salió
    de una pregunta sobre enlaces). `IKnowledgeHubStore` recibe `VisibilityFilter` únicamente en
    `GetVisiblePagesAsync`, `GetVisiblePageHeaderAsync` y `SearchPublishedAsync`; los ~30 restantes
    responden por clave primaria sin saber quién pregunta. **Donde el core no pone la guarda, no la
    pone nadie.** El agujero real: nadie reescribe los `<a href>` del cuerpo, así que el Guid de una
    página enlazada es público para quien lea la que enlaza, y con él `GetVersionsAsync(pagePk)` →
    `GetVersionContentAsync(versionPk)` devolvían el HTML íntegro **sin una sola comprobación**,
    borradores nunca publicados incluidos. Cerrado con `EnsureVisibleAsync` /
    `EnsureVersionVisibleAsync`; el segundo cuesta dos viajes porque **no existe forma de resolver
    versión → página filtrada**. Regla: *todo método que reciba un `versionPk` tiene que resolver su
    página y validarla*.
    **Trampa al cerrarlo, medida con el arnés**: una página recién creada nace `IsPublic = false` y
    **sin filas de permisos**, y la visibilidad era *admin OR pública OR tienes uno de sus permisos*
    → **era invisible incluso para quien la crea**. Poner la guarda en las rutas de gestión
    **bloqueaba al creador de su propia página**: 10 checks del arnés en rojo, y por eso la 0.15.0
    dejó la guarda solo en lectura y la escalada abierta. **Resuelto en la 0.16.0** (gotcha 32), y
    con el bloqueo fuera las guardas de gestión entraron enteras.
31. **Lo que NO protege el módulo, y conviene repetir antes de prometer nada**: el **texto de un
    enlace** a una página restringida queda a la vista (nadie toca el cuerpo, solo `docimg://`), y
    viaja también al PDF; las **imágenes** de `/kh/assets` se sirven por hash **sin autenticación**
    (256 bits: la URL ES la credencial) y sin forma de saber a qué página pertenecen — el anfitrión
    puede encadenar `.RequireAuthorization()`; y la **herencia de ancestros solo existe en
    `BuildTree`**, así que una página visible con padre invisible no sale en el árbol pero sí se lee
    por pk y sí aparece en la búsqueda.

32. **Una página nueva era invisible para su propio autor** (v0.16.0, reportado probando el demo con
    `editor1`: crear una página, publicarla y no verla NUNCA en el árbol). `CreatePageAsync` nunca
    asignó permisos, así que la página nace `IsPublic = false` y sin filas en `DocPagePermission`;
    publicar no toca la visibilidad. Con un rol Editor **la librería no servía para crear
    contenido**: todo lo creado desaparecía. Venía de la v0.1 y afectaba a los dos botones que crean
    páginas (`KnowledgeHubNavTree` para raíces, `KnowledgeHubPageManage` para subpáginas).
    Arreglado por dos lados: (a) una **subpágina hereda `IsPublic` y los permisos del padre** —con
    `GetPagePermissionsAsync`/`SetPagePermissionsAsync`, sin tocar el contrato del store—, y (b)
    `VisibilityFilter` gana **`SeesUnconfigured`** (con valor por defecto, así que ningún store
    propio se rompe), que enseña las páginas **sin configurar** a quien puede editar: están ocultas
    para todos, así que mostrarlas no destapa nada y evita que se pierdan.
    **Consecuencia sobre datos existentes**: al actualizar, las páginas sin configurar pasan a verlas
    los editores. Esconder por omisión deja de funcionar.
    Y lo importante de segundo orden: **esto desbloqueó las guardas de gestión** que la 0.15.0 tuvo
    que dejar fuera, así que la escalada de privilegios quedó cerrada en la misma entrega. La regla
    de tres casos (pública / concedida / sin configurar) vive en el XML doc de `VisibilityFilter` y
    hay que replicarla en los 4 proveedores.

33. **Un recorrido de subárbol nace inseguro: `GetActivePageLinksAsync` NO filtra por visibilidad**
    (v0.17.0, salió de preguntar «¿se bloquea el borrado si hay descendientes que no veo?» dando por
    hecho que sí). No se bloqueaba: **se borraban**. `DeletePageAsync` validaba con
    `EnsureVisibleAsync` **solo la pk que recibe**, y el subárbol lo calculaba sobre la lista cruda
    de enlaces —que no recibe `VisibilityFilter` y no puede recibirlo, porque si filtrara la cascada
    dejaría huérfanos—, así que un editor borraba una rama y destruía las páginas restringidas de
    debajo **en silencio**. El propio comentario del método lo admitía por escrito.
    Regla general: **`EnsureVisibleAsync` protege la pk de entrada, no la operación**. Cualquier cosa
    en cascada (borrar, y en su día mover o exportar en masa) tiene que comparar el conjunto
    resultante contra `GetVisiblePagesAsync` — para eso está `CountHiddenAsync`, que además
    cortocircuita para admin y no paga la consulta en el caso normal.
    **Limitación aceptada a propósito**: `MovePageAsync` sigue arrastrando descendientes invisibles
    (valida los dos extremos, y el subárbol cuelga por `ParentPk` sin enumerarse). No destruye nada y
    es reversible, pero puede reubicar contenido que no ves y, por la herencia de ancestros, cambiar
    a quién le aparece.
    De paso, **primer uso de la sobrecarga de 3 argumentos de `Returning.Unfinished(título, mensaje,
    tipo)`**: hasta la v0.17.0 todos los rechazos usaban la de 2 y el `Mensaje` viajaba vacío. No hay
    que tocar nada de UI —`NotifyExtensions` ya mapea `Title → Summary` y `Mensaje → Detail`— y el
    modo `http` del arnés confirma que el texto sobrevive el transporte.

32. **`RadzenTree` no tiene API pública de expansión, y su estado se destruye con los datos**
    (v0.18.0, medido sobre el paquete 11.1.5). Síntoma reportado: gestionar cualquier página
    reabría el árbol entero y el usuario perdía el sitio.
    - El único control soportado es **`RadzenTreeLevel.Expanded`**, un `Func<object,bool>` que se
      evalúa al **crear** cada `RadzenTreeItem`. Aquí estaba fijado a `_ => true`, así que no es que
      se «perdiera» el estado: es que **se reexpandía todo**.
    - `RadzenTree` **no** expone `ExpandAll`, `ExpandedItems` ni métodos de expansión;
      `ExpandItem`/`ExpandCollapse` son `internal`. `Reload()` **no expande**: solo rehidrata hijos
      perezosos. Los eventos `Expand`/`Collapse` son la ÚNICA forma de saber qué toca el usuario.
    - El estado vive en campos privados del `RadzenTreeItem` (`expanded`/`clientExpanded`), y el
      render **keyea cada item por la instancia de datos** (`builder.SetKey(data)`). Como
      `GetTreeAsync` devuelve DTOs nuevos en cada carga, las keys cambian, los items se destruyen y
      el estado se va con ellos. Ni un `@key` por `Pk` lo salvaría: el key lo pone Radzen.
    - Conclusión general: con este árbol, **cualquier estado de UI por nodo hay que mantenerlo
      fuera del componente** y devolverlo por los `Func<object,bool>` de `RadzenTreeLevel`.
    Y un bug en dirección contraria que salió al mirarlo: la pantalla de permisos **no llamaba a
    `NotifyPageTreeChanged`**, así que cambiar la visibilidad dejaba el árbol desactualizado hasta
    pulsar 🔄.

33. **Meter un motor de plantillas dentro de HTML saneado: cuatro cosas medidas** (v0.19.0).
    - **El saneador destrozaba las TABLAS, no el `<`.** AngleSharp aplica las reglas de parseo de
      tabla y **expulsa fuera** cualquier texto que no esté en una celda: un `{{ for }}` envolviendo
      `<tr>` salía de la tabla y dejaba de envolver nada. Era el caso de uso principal (equipos con
      IP, roles en tabla) y el spike lo cazó antes de escribir el motor.
    - **Solución: convertir cada `{{ … }}` en un COMENTARIO HTML antes de sanear y restaurarlo
      después.** Los comentarios no se expulsan de una tabla. Medido: tabla con bucle, `< 5`, `<b`
      pegado y `&&` quedan **intactos**, y `<script>`/`onclick` **siguen muriendo**. Con esto
      sobraron la decodificación de entidades y las funciones `lt`/`gt` que el plan preveía.
    - **La protección NO puede ser ciega.** Protegiendo siempre, un `{{ <script>alert(1)</script> }}`
      en una página SIN plantillas sobreviviría al saneado y se ejecutaría al mostrarla. Por eso la
      sobrecarga lleva `preserveTemplateSyntax` y **solo se activa en páginas marcadas**, donde
      Scriban consume ese texto y jamás lo emite. Y **al desmarcar hay que volver a sanear** el
      contenido guardado, o quedaría almacenado sin sanear y ya sin nadie que lo renderice.
    - **Scriban ≤ 7.1.0 tiene 2 avisos de gravedad ALTA**, y uno es directamente relevante:
      `array.insert_at` **ignora `LoopLimit` y `LimitToString`** → OOM del proceso pese al contexto
      endurecido. Corregido en 7.2.0; se usa la **7.2.6**. Con `TreatWarningsAsErrors` la 7.1.0 ni
      compila, así que el aviso NU1903 es la red de seguridad.
    - Del propio Scriban, verificado leyendo su fuente (viene en el paquete): **no expone métodos de
      instancia** (solo campos y propiedades públicas), `include` es inerte sin `TemplateLoader`,
      `CancellationToken` se comprueba **en cada sentencia** (funciona en render síncrono), y
      `LimitToString` es un presupuesto **acumulativo** que solo se reinicia con `Reset()` → hay que
      crear un `TemplateContext` NUEVO por render o la segunda página sale truncada en silencio.
    - **`kh.is_pdf` no se puede resolver con un parámetro opcional en la lectura**: esa la llama
      todo el mundo y el que lo olvide produce un fallo silencioso. Va en un método aparte,
      `GetPageForExportAsync`, que solo usa el exportador. El arnés cazó justo ese bug.

34. **Un comentario Razor DENTRO de la lista de atributos de un componente compila y revienta al
    renderizar** (v0.19.2). Razor no se queja: lo trata como el **nombre de un atributo**, así que
    falla en ejecución con *"does not have a property matching the name '@*…'"*. Rompió el
    `KnowledgeHubBrowser` entero —la integración de una línea que anuncia el README— durante **tres
    versiones**, porque 0 warnings no dice nada de esto. Los comentarios van **entre elementos**,
    nunca entre atributos. Cubierto por el humo de bUnit.

35. **`RadzenTree` vuelve a disparar `Change` al reaplicar la selección tras recargarse** (v0.19.3),
    exactamente igual que si el usuario hubiera hecho clic. Con el predicado `Selected` que se añadió
    en la v0.18.0, cualquier recarga del árbol informaba de una selección que solo estaba
    **restaurando**, y eso arrastraba la vista del anfitrión: pulsar Editar cambiaba a la vista de
    edición y el refresco del árbol la devolvía al lector, **sin error en ninguna parte**. Guardado
    en `OnNodeSelect`: un eco siempre nombra la página que YA es la actual, así que ese es el caso a
    ignorar. Afectaba a más sitios que Editar —guardar el icono en Gestionar también recarga el
    árbol y también te expulsaba—, y **solo al modo embebido**: en enrutado nadie pasaba
    `CurrentPagePk`, el predicado no marcaba nada y no había eco.
    Diagnóstico que lo desatascó: editar el DOM a mano con las dev tools y ver que se revertía
    demostró que el manejador SÍ corría; a partir de ahí, instrumentar y leer la traza.
    **Y ese «solo al modo embebido» era el otro bug** (v0.19.4): en el portal el árbol **no marcaba
    nunca la página abierta**, ni al llegar por URL ni al navegar, y 🔄 o guardar en
    Permisos/Gestionar «perdía» la marca. El layout del portal no puede pasar `CurrentPagePk` —la
    página ruteada vive dentro de `@Body`—, así que la única fuente es la URL, que el árbol ya leía
    para abrir la rama (`PageFromUrl`) pero no para marcar. Ahora ambos y la guarda del eco leen un
    único `ActivePagePk = CurrentPagePk ?? PageFromUrl()`.
    Lo que había que saber de Radzen para arreglarlo, leído en su fuente:
    - `RenderTreeItem` hace `builder.SetKey(data)` y `Selected = Value == data || selected(data)`.
      Como cada `GetTreeAsync` devuelve DTOs nuevos, al recargar mueren todos los items y
      `RadzenTree.SelectedItem` **queda apuntando a un item destruido**, que nadie limpia. Por eso
      el resaltado del clic no sobrevive a una recarga: lo único que lo repone es el predicado.
    - `RadzenTreeItem.SetParametersAsync` **sí** reacciona a que cambie `Selected`
      (`DidParameterChange` → `selected = …; Tree?.SelectItem(this)`), así que la marca se mueve con
      un simple re-render y navegar **no cuesta una consulta al store**.
    - `SelectItem` no dispara `Change` si el item ya era el `SelectedItem`, así que «clic en el nodo
      que ya es el activo no hace nada» es de Radzen tanto como de la guarda. Es el precio asumido:
      de Gestionar/Editar se sale por su botón Volver, no clicando la página en el árbol.
    - **Y ese `SelectedItem` se queda colgado**: nadie lo limpia al deseleccionar, así que tras
      volver a `/kh` el árbol se quedaba MUDO —clic en la página, resaltado sí, navegación no—.
      Lo único que lo limpia es `Value`: `SetParametersAsync` hace `SelectedItem = null` cuando
      **pasa a null**. De ahí `Value="@ActiveNode"`, que no duplica al predicado: el predicado marca,
      `Value` desmarca. Existía desde siempre, pero marcar desde la URL lo agravaba (ahora hay
      selección sin que nadie haya hecho clic), así que entra en la misma entrega.
    - Distinguir el eco con una bandera de render **no funciona aquí**: el item hace
      `await Tree.ExpandItem(this)` ANTES de seleccionar, y ese await pasa por nuestro `OnNodeExpand`
      → `PersistCollapsedAsync` → interop JS, así que el eco puede llegar después del render.
    Cubierto por 6 pruebas de bUnit sobre `aria-selected` (no sobre clases de Radzen), y verificado
    en el demo Server: llegar por URL marca, navegar mueve la marca **sin recargar el árbol**, 🔄 la
    conserva y el clic vuelve a navegar.

36. **Un índice por página no puede vivir en el contenido guardado, y las container queries tienen
    una regla que no se ve venir** (v0.20.0).
    - **El saneador borra `id` en los TRES niveles** (medido con Ganss.Xss 9.1.968-beta: `id` no está
      en los 95 `AllowedAttributes` de fábrica, `KnowledgeHubSanitizerDefaults` solo añade `class`, y
      el nivel 3 sustituye la lista entera por `src/alt/style`). Como `SaveDraftAsync` sanea en cada
      guardado, un ancla escrita a mano en el HTML **vive hasta el siguiente guardado y desaparece
      sin decir nada**. Por eso `BuildOutline` inyecta los `id` al renderizar y nunca los persiste —
      y por eso, gratis, el índice funciona en páginas escritas mucho antes de que existiera.
      Curiosidad útil: `name` y `href="#…"` SÍ sobreviven a los niveles 1 y 2, así que el enlace vive
      y el destino muere. Un anfitrión puede permitir `id` con `ConfigureStandard`, así que
      `BuildOutline` **reutiliza** el `id` que ya venga en vez de añadir un segundo.
    - **Regex sobre HTML, con una trampa concreta**: `<h2[^>]*>` parece bastar y **corrompe el
      marcado** en cuanto un atributo lleva `>` entre comillas (`title="a > b"`), porque el `id` se
      inyecta en mitad del atributo. La regex consume los valores entrecomillados enteros. Y **sin
      backreferencia** en el cierre: el generador de `[GeneratedRegex]` puede emitir SYSLIB1044, que
      aquí es error de compilación. Solo se reescribe si han casado apertura Y cierre, así que un
      encabezado sin cerrar cuesta un enlace, nunca HTML roto.
    - **Una container query NO puede dar estilo a su propio contenedor.** Se resuelve contra el
      contenedor ANCESTRO, así que un `@container { .el-contenedor { flex-wrap: wrap } }` se ignora
      **en silencio** — y aquí eso dejaba la columna de texto en **0 px** en el modo estrecho. Hay
      que separar en dos elementos: uno declara `container-type` y mide, otro obedece
      (`.kh-doc-layout` / `.kh-doc-row`). Lo contrario también muerde: mover el `flex-wrap` a la
      regla base parece equivalente y no lo es, porque en modo ancho hace que el panel salte de
      línea. Lo que sí se confirmó: `container-type: inline-size` **no** rompe el `position: sticky`.
    - **`scrollIntoView({behavior:'smooth'})` puede aceptarse y no animar nada** (navegadores
      automatizados, algunos WebView). Devuelve sin error y el lector se queda donde estaba. Se
      pide smooth y, si a los 250 ms no se ha movido nada, se salta en seco: aterrizar importa más
      que deslizarse. `behavior:'auto'` funcionó siempre.
    - **El scroll-spy NO usa `IntersectionObserver`**, por dos razones y solo una es de entorno. La
      de diseño: un observador sobre una banda superior **no marca NADA** cuando ningún encabezado
      cae dentro —página corta, o sección que llena la pantalla—, justo cuando más metido en el
      texto estás. Con geometría siempre hay respuesta: el último encabezado que pasó la banda. La
      de entorno: el panel del navegador de esta sesión **no entrega ni una entrada** de
      `IntersectionObserver`, ni con opciones por defecto (tampoco compone frames ni deja capturar).
      Y la regla dura: el spy **no llama a .NET jamás** — un aviso por sección cruzada sería un
      round-trip de SignalR por sección mientras arrastras la barra (gotcha 18).
    - Los enlaces del panel son `<button>`, no `<a href="#ancla">`: el router de Blazor intercepta
      los fragmentos, cambiaría la URL —lo que despierta al árbol, que escucha `LocationChanged`— y
      encima no haría scroll, porque quien scrollea es un div y no la ventana. Verificado en el demo
      embebido: saltar **no cambia** `/mi-app/documentacion`.

37. **Un menú contextual depende de un componente que la librería NO renderiza, y un enlace pegado
    entre páginas rompe justo en el modo más común** (v0.21.0).
    - **`<RadzenComponents />` monta CINCO hosts** —`RadzenDialog`, `RadzenNotification`,
      `RadzenContextMenu`, `RadzenTooltip`, `RadzenChartTooltip`— y lo pone el **anfitrión**, junto
      al Router. La RCL no renderiza ninguno: ya dependía de ello para diálogos y notificaciones.
      `ContextMenuService.Open` es `OnOpen?.Invoke(...)`, así que **sin el host no lanza: no hace
      nada**. La diferencia de gravedad con un diálogo es que `RadzenTreeItem` ya hace
      `preventDefault` del `oncontextmenu`, así que el usuario pierde también el menú nativo y se
      queda sin nada. De ahí `TreeContextMenu`, para poder apagarlo.
    - **El arnés de bUnit no lo veía**: registraba los servicios de Radzen pero no montaba
      `<RadzenComponents />`, así que un menú que no abre habría pasado por verde. Ahora
      `RenderRadzenOverlays()` lo monta, y el menú se lee de **su** markup, no del componente que lo
      pidió.
    - **El callback de `ContextMenuService.Open` es SÍNCRONO** (`Action<MenuItemEventArgs>`) y todo
      lo que cuelga de él es async (portapapeles, crear página) → hay que marshalar con `InvokeAsync`
      (gotcha 11). Y `ContextMenuItem` **no admite hijos**: para submenús hay que usar la otra
      sobrecarga con `RenderFragment`.
    - **El portapapeles necesita contexto seguro Y foco.** `navigator.clipboard.writeText` lanza
      `NotAllowedError: Document is not focused` con la pestaña en segundo plano, y el respaldo
      `document.execCommand('copy')` también falla sin foco. Por eso `copyText` devuelve un booleano
      y la UI, si es `false`, **enseña el texto en la notificación** en vez de mentir. Medido: en el
      panel de esta sesión `isSecureContext` es true pero `document.hasFocus()` es false, así que el
      camino feliz no se pudo verificar aquí — el de respaldo sí, y funciona.
    - **Interceptar enlaces: lo que NO hay que interceptar.** Clic con ctrl/cmd/shift/alt, botón
      central, `target` distinto de `_self`, `download`, y cualquier href de otro origen. Todo eso
      son cosas que el navegador ya le da al lector y quitárselas no gana nada. Verificado en el demo:
      externo `false`, ctrl+clic `false`, clic normal `true`.
    - **El mismo-origen se decide en JS, no en C#**: `TryGetPagePk` solo ve un path, así que aceptar
      un `/kh/page/{pk}` de otro dominio sería cosa suya. El JS resuelve con `new URL(...)` y compara
      `origin` antes de retener el clic.
    - Resultado medido en el demo embebido: clic en un enlace entre páginas y la URL **sigue siendo**
      `/mi-app/documentacion`, el contenido cambia, el árbol marca el destino y la topbar del
      anfitrión sigue ahí. En portal navega sin recarga completa (`navigation` entries no sube).

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
