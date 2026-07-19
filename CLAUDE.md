# CLAUDE.md — MgSoftDev.KnowledgeHub

Contexto del proyecto para asistentes de código. Léelo al iniciar sesión.

## Qué es

**MgSoftDev.KnowledgeHub**: familia de paquetes NuGet que empaqueta el módulo de documentación
colaborativa validado en `../DocBookDemo` (DocsPortal, que queda INTACTO como referencia).
Multi-motor de BD (SQL Server, LiteDB; PostgreSQL futuro) y multi-hosting (WPF BlazorWebView,
Blazor Server, Blazor WASM). Estado: **10 fases completas y verificadas**, v0.1.0-preview.1.

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
  - CSS: alturas por variables `--kh-portal-height` / `--kh-editor-height` (default `100vh`);
    `KnowledgeHubBrowser` usa `.kh-embedded` (100% del contenedor).
  - **`Options.HeaderActionsComponent` lo renderiza `KnowledgeHubNavTree`** (no el layout), que
    es el único componente presente en los tres modos → el gancho funciona siempre. Compone con
    el `FooterContent` del árbol (primero el del anfitrión, luego el gancho). `KnowledgeHubLayout`
    NO debe renderizarlo (sería doble). `KnowledgeHubBrowser` expone `TreeFooterContent` como
    passthrough al `FooterContent` del árbol.
- **Editor tools**: `EditorToolDescriptor` en `KnowledgeHubBlazorOptions.EditorTools`; los 4
  callouts built-in se registran por el mismo mecanismo (removibles).

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
Demos/SharedAuth/     auth de demo compartida (users/roles LiteDB propio + AdminUsers/HostLinks/RootRedirect + SerilogReturningLoggerService)
Demos/Wpf/            anfitrión WPF+LiteDB (TFM net10.0-windows10.0.19041.0, virtual host docs-assets)
Demos/BlazorServer/   anfitrión Server+LiteDB (cookie auth, patrón AcceptsInteractiveRouting, puerto 5210)
Demos/Wasm(+.Server)/ anfitrión WASM hosted + API (token opaco en memoria, puerto 5220)
Tests/KnowledgeHub.ParityHarness/  guion de paridad: modos inmemory|litedb|sqlserver|http
.github/workflows/publish-nuget.yml  CI: publica a nuget.org al pushear tag v* (Trusted Publishing/OIDC)
artifacts/       feed NuGet local, git-ignored (dotnet pack -c Release -o artifacts)
```

## Publicación (nuget.org)

Los 9 paquetes están **publicados en nuget.org** (perfil `migeru_garcia`), primera versión
`0.1.0-preview.1`. La publicación es automática: `.github/workflows/publish-nuget.yml` se dispara
al pushear un tag `v*`, empaqueta los 9 proyectos de librería (glob `MgSoftDev.KnowledgeHub*/*.csproj`
en ubuntu-latest — NO `dotnet pack` del `.slnx`, que arrastraría el demo WPF `net10.0-windows`
que no compila en Linux), toma la versión del tag (`-p:Version=${GITHUB_REF_NAME#v}`), y sube con
**Trusted Publishing (OIDC)** vía `NuGet/login@v1` (usuario `migeru_garcia`, sin API keys). Nueva
release = tag/versión nuevo (nuget.org no permite re-publicar una versión existente).

## Verificación (cómo se probó)

- **Guion de paridad** (46 checks): mismo guion contra InMemory, LiteDB, SQL Server
  (`DEVSQL2022`, BD temporal `KnowledgeHubParity`) y a través de HTTP (Kestrel real).
  `dotnet run --project Tests/KnowledgeHub.ParityHarness -- <modo>`; sqlserver necesita
  `KH_SQLSERVER_CS` y BD vacía; http levanta Kestrel en 127.0.0.1:5599.
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
