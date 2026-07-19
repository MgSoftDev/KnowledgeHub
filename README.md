# MgSoftDev.KnowledgeHub

> Módulo reutilizable de **documentación colaborativa** para .NET 10 — multi-motor de base de
> datos y multi-hosting.

![status](https://img.shields.io/badge/estado-v0.1.0--preview.1-blue)
![net](https://img.shields.io/badge/.NET-10-512BD4)
![license](https://img.shields.io/badge/licencia-MIT-green)

KnowledgeHub empaqueta un portal de documentación colaborativa —árbol jerárquico de páginas con
visibilidad por permisos y herencia, editor HTML con herramientas personalizables, versionado
insert-only con historial y publicación atómica (con detección de conflictos entre usuarios),
búsqueda, e imágenes WebP deduplicadas por hash con caché— como una **familia de paquetes NuGet
embebibles** en aplicaciones anfitrionas.

Fue extraído del demo de referencia `DocsPortal` a una librería desacoplada que corre igual en
**WPF (BlazorWebView)**, **Blazor Server** y **Blazor WebAssembly + Web API**, sobre **SQL
Server** o **LiteDB** (y extensible a otros motores). La aplicación anfitriona conserva su propio
sistema de usuarios: solo le dice a KnowledgeHub quién es el usuario y qué permisos tiene.

## Dos formas de usarlo

**1. Embebido en tu app** (dentro de tu layout, con tu menú y topbar) — una línea:

```razor
@page "/documentacion"
<KnowledgeHubBrowser Title="Documentación" />
```

También puedes componer a tu medida (`KnowledgeHubNavTree` en tu sidebar, `KnowledgeHubPageView`
en tu contenido, el editor en una pestaña…). Ver la
[guía §3.5](GUIA-IMPLEMENTACION.md#35-dos-modos-de-integración-portal-vs-embebido).

**2. Portal llave en mano** — añades la RCL al Router y quedan listas las rutas `/kh/*`
(`/kh/page/{id}`, `/kh/edit/{id}`, …), que adoptan el layout que fije tu Router.

## Documentación

- **[GUIA-IMPLEMENTACION.md](GUIA-IMPLEMENTACION.md)** — guía paso a paso para integrar la
  librería en cada hosting (con código copy-paste, troubleshooting y checklist).
- **[CLAUDE.md](CLAUDE.md)** — contexto técnico y de arquitectura (decisiones, gotchas).

## Paquetes

| Paquete | Qué contiene |
|---|---|
| `MgSoftDev.KnowledgeHub.Abstractions` | Contratos, entidades y DTOs (para stores/adaptadores propios) |
| `MgSoftDev.KnowledgeHub` | Servicios core (lógica de negocio) |
| `MgSoftDev.KnowledgeHub.Storage.LiteDb` | Provider LiteDB (persistencia en archivo, sin servidor) |
| `MgSoftDev.KnowledgeHub.Storage.EntityFramework` | Base EF neutral (para providers relacionales) |
| `MgSoftDev.KnowledgeHub.Storage.SqlServer` | Provider SQL Server + script DDL (esquema/prefijo configurable) |
| `MgSoftDev.KnowledgeHub.Blazor` | La UI: Razor Class Library (11 componentes + editor con herramientas inyectables) |
| `MgSoftDev.KnowledgeHub.AspNetCore` | Endpoint de imágenes `/kh/assets` (caché `immutable`) |
| `MgSoftDev.KnowledgeHub.Http.Server` | API minimal de los contratos (para clientes WASM) |
| `MgSoftDev.KnowledgeHub.Http.Client` | Implementaciones `HttpClient` de los contratos (WASM-safe) |

### Qué instalar según el hosting

| Hosting | Paquetes |
|---|---|
| **WPF** (BlazorWebView) | `KnowledgeHub` + `Storage.*` + `Blazor` |
| **Blazor Server** | `KnowledgeHub` + `Storage.*` + `Blazor` + `AspNetCore` |
| **WASM** (cliente) | `Http.Client` + `Blazor` |
| **WASM** (server API) | `KnowledgeHub` + `Storage.*` + `AspNetCore` + `Http.Server` |

## Estructura del repositorio

```
MgSoftDev.KnowledgeHub.slnx        Solución (.NET 10, Central Package Management)
MgSoftDev.KnowledgeHub.*/          Los 9 proyectos de librería
Demos/                             3 apps anfitrionas completas y funcionales:
  KnowledgeHub.Demo.Wpf                WPF + LiteDB
  KnowledgeHub.Demo.BlazorServer       Blazor Server + LiteDB
  KnowledgeHub.Demo.Wasm(+ .Server)    Blazor WASM hosted + Web API
  KnowledgeHub.Demo.SharedAuth         Auth de demo compartida (usuarios/roles del anfitrión)
Tests/KnowledgeHub.ParityHarness/  Arnés de paridad (46 checks) sobre 4 modos de store
```

## Compilar y probar

```bash
# Compilar toda la solución (0 warnings esperado)
dotnet build MgSoftDev.KnowledgeHub.slnx

# Arnés de paridad — mismo guion sobre distintos motores
dotnet run --project Tests/KnowledgeHub.ParityHarness -- inmemory
dotnet run --project Tests/KnowledgeHub.ParityHarness -- litedb
# sqlserver: requiere env KH_SQLSERVER_CS y una BD vacía
# http:      levanta un Kestrel real y prueba a través de Http.Client

# Empaquetar al feed local (carpeta artifacts/)
dotnet pack -c Release -o artifacts
```

Cada demo se ejecuta con `dotnet run --project Demos/<nombre>` (el WASM se sirve desde su
proyecto `.Server`).

## Requisitos

- **.NET 10 SDK**
- Para la UI: **Radzen.Blazor** (lo traen los paquetes; el anfitrión referencia su tema).
- Para el provider SQL Server: una instancia de **SQL Server** (la BD debe existir; las tablas
  las crea el script DDL embebido).

## Instalación

Los paquetes están **publicados en nuget.org** (perfil
[migeru_garcia](https://www.nuget.org/profiles/migeru_garcia)). Instala los que necesites según
tu hosting (ver la [guía](GUIA-IMPLEMENTACION.md#2-los-paquetes-y-cuál-instalar)):

```bash
dotnet add package MgSoftDev.KnowledgeHub
dotnet add package MgSoftDev.KnowledgeHub.Storage.LiteDb   # o .Storage.SqlServer
dotnet add package MgSoftDev.KnowledgeHub.Blazor
```

### Publicación (mantenedores)

La publicación a nuget.org es automática vía GitHub Actions con **Trusted Publishing (OIDC)** —
sin API keys. Se dispara al pushear un tag `v*`, y la versión sale del tag (`v1.2.3` → `1.2.3`,
misma versión para los 9 paquetes):

```bash
git tag v1.2.3
git push origin v1.2.3
```

Workflow: [`.github/workflows/publish-nuget.yml`](.github/workflows/publish-nuget.yml).

## Estado

**v0.1.0-preview.1** (primera publicación en nuget.org). Las 9 librerías + 3 demos compilan sin
warnings; el arnés de paridad pasa 46/46 en los 4 modos de store; los demos están verificados
end-to-end.

## Licencia

MIT.
