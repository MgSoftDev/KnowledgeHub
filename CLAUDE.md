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
artifacts/       feed NuGet local (dotnet pack -c Release -o artifacts)
```

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

## Pendientes / siguientes pasos

- Captura visual del demo WPF (la sesión de Windows estaba bloqueada durante la verificación;
  el arranque/seed/auto-login se verificó por log). Env vars DEBUG: `KNOWLEDGEHUB_AUTOLOGIN=admin`,
  `KNOWLEDGEHUB_STARTPAGE=<slug|/ruta>`.
- Posible: provider PostgreSQL sobre Storage.EntityFramework; ContractTests xunit permanentes;
  persistir token WASM en sessionStorage; publicar a nuget.org (quitar -preview).
- **No es repo git** todavía.
