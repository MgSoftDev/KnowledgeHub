# Guía de implementación — MgSoftDev.KnowledgeHub

Guía paso a paso para integrar el módulo de documentación colaborativa **KnowledgeHub** en una
aplicación anfitriona. Cubre los tres hostings soportados (**WPF + BlazorWebView**, **Blazor
Server** y **Blazor WebAssembly + Web API**), los dos storage providers oficiales (**LiteDB** y
**SQL Server**) y la implementación de un storage propio. Está escrita para que cualquier
persona —o asistente de IA— pueda seguirla de principio a fin sin conocer el código fuente de
la librería.

> **Apps de referencia:** cada hosting tiene un demo completo y funcional en `Demos\`
> (`KnowledgeHub.Demo.Wpf`, `KnowledgeHub.Demo.BlazorServer`, `KnowledgeHub.Demo.Wasm` +
> `KnowledgeHub.Demo.Wasm.Server`). Ante cualquier duda de esta guía, el demo es la verdad.

---

## Índice

1. [Qué es y qué incluye](#1-qué-es-y-qué-incluye)
2. [Los paquetes y cuál instalar](#2-los-paquetes-y-cuál-instalar)
3. [Conceptos obligatorios antes de empezar](#3-conceptos-obligatorios-antes-de-empezar)
   - 3.1 El contrato de identidad `IKnowledgeHubUserContext`
   - 3.2 Permisos reservados y visibilidad por página
   - 3.3 El pipeline de imágenes y `PublicAssetsBaseUrl`
   - 3.4 Requisitos de la UI (RCL)
4. [Storage: elegir y configurar el proveedor](#4-storage-elegir-y-configurar-el-proveedor)
   - 4.1 LiteDB
   - 4.2 SQL Server
   - 4.3 Implementar un store propio
5. [Implementación en WPF (BlazorWebView)](#5-implementación-en-wpf-blazorwebview)
6. [Implementación en Blazor Server](#6-implementación-en-blazor-server)
7. [Implementación en Blazor WebAssembly + Web API](#7-implementación-en-blazor-webassembly--web-api)
8. [Herramientas personalizadas del editor HTML](#8-herramientas-personalizadas-del-editor-html)
9. [Seeding de contenido de ejemplo](#9-seeding-de-contenido-de-ejemplo)
10. [Permisos finos (opt-in)](#10-permisos-finos-opt-in)
11. [Solución de problemas (gotchas)](#11-solución-de-problemas-gotchas)
12. [Checklist final de verificación](#12-checklist-final-de-verificación)

---

## 1. Qué es y qué incluye

KnowledgeHub es un **módulo embebible de documentación colaborativa**: árbol jerárquico de
páginas con visibilidad por permisos (con herencia), editor HTML (Radzen) con herramientas
personalizables, versionado insert-only con historial y restauración, publicación atómica con
detección de conflictos entre usuarios, búsqueda, imágenes WebP deduplicadas por hash con
caché, y panel de diagnóstico.

Principios de diseño que afectan a tu integración:

- **KnowledgeHub NO tiene tablas de usuarios.** Tu aplicación (el "anfitrión") le dice quién
  es el usuario y qué permisos tiene, a través de una interfaz que tú implementas (§3.1).
- **La persistencia es intercambiable.** Instalas un provider oficial (LiteDB o SQL Server) o
  implementas la interfaz del store tú mismo (§4.3).
- **La UI es una Razor Class Library** que corre idéntica en WPF, Blazor Server y WASM. Sus
  rutas viven bajo el prefijo fijo **`/kh`** y sus estilos usan clases `kh-*`, para no
  colisionar con tu app.
- **Todo método que puede fallar devuelve `Returning`/`Returning<T>`** (MgSoftDev.Returning),
  nunca excepciones sueltas ni `bool`.

Requisitos: **.NET 10**, y para la UI **Radzen.Blazor** (lo trae el paquete Blazor).

---

## 2. Los paquetes y cuál instalar

| Paquete | Qué contiene | Quién lo instala |
|---|---|---|
| `MgSoftDev.KnowledgeHub.Abstractions` | Contratos, entidades, DTOs | Llega transitivo; directo solo si implementas un store/adaptador propio |
| `MgSoftDev.KnowledgeHub` | Servicios core (lógica de negocio) | Todo anfitrión que corre la lógica in-process (WPF, Server, el server de una API) |
| `MgSoftDev.KnowledgeHub.Storage.LiteDb` | Provider LiteDB (archivo) | Anfitriones sin BD propia |
| `MgSoftDev.KnowledgeHub.Storage.SqlServer` | Provider SQL Server + DDL | Anfitriones con SQL Server |
| `MgSoftDev.KnowledgeHub.Blazor` | La UI (RCL, 11 componentes) | Todo anfitrión que muestra la UI |
| `MgSoftDev.KnowledgeHub.AspNetCore` | Endpoint de imágenes `/kh/assets` | Blazor Server y servers de API |
| `MgSoftDev.KnowledgeHub.Http.Server` | API minimal de los contratos | El server que atiende clientes WASM |
| `MgSoftDev.KnowledgeHub.Http.Client` | Contratos sobre HttpClient | El cliente WASM |

**Matriz por hosting:**

| Hosting | Paquetes |
|---|---|
| WPF | `KnowledgeHub` + `Storage.*` + `Blazor` (+ `Microsoft.AspNetCore.Components.WebView.Wpf`) |
| Blazor Server | `KnowledgeHub` + `Storage.*` + `Blazor` + `AspNetCore` |
| WASM (cliente) | `Http.Client` + `Blazor` |
| WASM (server API) | `KnowledgeHub` + `Storage.*` + `AspNetCore` + `Http.Server` |

**Instalar desde nuget.org.** Los paquetes están publicados (perfil `migeru_garcia`), así que se
instalan como cualquier otro:

```bash
dotnet add package MgSoftDev.KnowledgeHub
dotnet add package MgSoftDev.KnowledgeHub.Storage.LiteDb   # el provider que uses
dotnet add package MgSoftDev.KnowledgeHub.Blazor
```

o con `PackageReference`:

```xml
<PackageReference Include="MgSoftDev.KnowledgeHub" Version="0.1.0-preview.1" />
```

> **Feed local (opcional, solo para desarrollo del propio KnowledgeHub).** Si trabajas contra una
> versión no publicada, empaqueta con `dotnet pack -c Release -o artifacts` y añade en tu
> `nuget.config` una fuente apuntando a la ruta **absoluta** de esa carpeta `artifacts`. No uses
> una ruta relativa `artifacts` en un repo (rompería el restore en un checkout limpio, porque la
> carpeta está en `.gitignore` y solo la crea `dotnet pack`).

---

## 3. Conceptos obligatorios antes de empezar

### 3.1 El contrato de identidad `IKnowledgeHubUserContext`

Es **la** pieza que TÚ implementas siempre. KnowledgeHub la consulta para saber quién está
usando el módulo y qué puede hacer:

```csharp
public interface IKnowledgeHubUserContext
{
    bool IsAuthenticated { get; }

    /// Clave estable del usuario. Se guarda en la auditoría (RowUserCreate/RowUserUpdate)
    /// y se muestra como autor en el historial de versiones.
    string UserName { get; }

    /// Nombre amigable mostrado en el sidebar.
    string DisplayName { get; }

    /// Permisos efectivos del usuario (strings), INCLUYENDO los reservados KnowledgeHub.*
    /// cuando corresponda. La visibilidad de páginas se resuelve contra esta lista.
    IReadOnlyList<string> Permissions { get; }

    /// Catálogo de permisos del anfitrión, para el selector de visibilidad por página.
    Task<ReturningList<PermissionInfo>> GetPermissionCatalogAsync();
}
// PermissionInfo es: public sealed record PermissionInfo(string Name, string DisplayName);
```

**Vida del registro según hosting** (regla de oro):

| Hosting | Lifetime | Por qué |
|---|---|---|
| WPF | `Singleton` mutable (con `SetUser`/`Clear`) | Un proceso = un usuario logueado |
| Blazor Server | `Scoped` (por circuito) leyendo el principal/cookie | Un circuito = un navegador/usuario |
| API (para WASM) | `Scoped` (por request) resolviendo el token | Cada request trae su identidad |
| WASM (cliente) | `Singleton` poblado desde `GET /kh/api/me` | Es solo para la UI; la autoridad es el server |

**Patrón de mapeo roles → permisos.** Si tu sistema tiene roles (lo más común), expón cada rol
como un permiso `"Role.{Nombre}"` y otorga los reservados desde los roles apropiados:

```csharp
public static class MyAppPermissions
{
    public const string RolePrefix = "Role.";

    public static IReadOnlyList<string> ForRoles(IEnumerable<string> roleNames)
    {
        var permissions = new List<string>();
        foreach (var role in roleNames)
        {
            permissions.Add(RolePrefix + role);                          // para visibilidad por página
            if (role.Equals("Admin", StringComparison.OrdinalIgnoreCase))
                permissions.Add(KnowledgeHubPermissions.Admin);           // capacidad total
            if (role.Equals("Editor", StringComparison.OrdinalIgnoreCase))
                permissions.Add(KnowledgeHubPermissions.Edit);            // puede editar
        }
        return permissions;
    }
}
```

Y el catálogo (lo que ve el picker de visibilidad) son tus roles proyectados a
`PermissionInfo("Role.Editor", "Editor")`. Si tu sistema ya maneja permisos granulares en vez
de roles, pásalos directamente — para KnowledgeHub son strings opacos.

### 3.2 Permisos reservados y visibilidad por página

La librería entiende 4 nombres *well-known* (constantes en `KnowledgeHubPermissions`):

| Constante | Valor | Capacidad |
|---|---|---|
| `Admin` | `KnowledgeHub.Admin` | Ve TODAS las páginas y puede todo |
| `Edit` | `KnowledgeHub.Edit` | Crear/editar/guardar borradores. En el modo por defecto también publica, gestiona páginas y visibilidad |
| `Publish` | `KnowledgeHub.Publish` | Publicar — **solo se exige** con `UseFineGrainedPublish` (§10) |
| `ManagePermissions` | `KnowledgeHub.ManagePermissions` | Gestionar visibilidad — solo con `UseFineGrainedManagePermissions` |

**Visibilidad de una página** (para lectura): una página es visible si `IsPublic == true`, o si
alguno de sus permisos asignados (strings de TU catálogo, p. ej. `Role.Produccion`) está en la
lista `Permissions` del usuario (comparación case-insensitive). La herencia aplica al árbol: si
el padre no es visible, los hijos tampoco se muestran. `KnowledgeHub.Admin` ve todo.

Los servicios **validan permisos server-side** en cada mutación — la UI solo refleja lo mismo.
Nunca dependas de esconder botones.

### 3.3 El pipeline de imágenes y `PublicAssetsBaseUrl`

El HTML almacenado referencia imágenes como `docimg://{id}` (estable). Al mostrar, la librería
las reescribe a una URL de display `{PublicAssetsBaseUrl}/{hash}.webp`; al guardar, cualquier
URL de display se normaliza de vuelta a `docimg://` automáticamente. Tu única responsabilidad
es configurar el valor correcto y servir esas URLs:

| Hosting | `PublicAssetsBaseUrl` | Quién sirve la imagen |
|---|---|---|
| WPF | `https://docs-assets` | El virtual host de WebView2, mapeado a la carpeta de caché (§5, paso 5) |
| Blazor Server | `/kh/assets` | `app.MapKnowledgeHubAssets()` del paquete AspNetCore |
| WASM | `/kh/assets` (mismo origen, hosted) | El server de la API con `MapKnowledgeHubAssets()` |

`AddKnowledgeHubFileImageCache(folder)` registra el caché de disco local (direccionado por
hash: dedup e invalidación automáticas, sin expiración). Regístralo en WPF y en cualquier
server; **no** en el cliente WASM (ahí cachea el navegador gracias al header `immutable`).

### 3.4 Requisitos de la UI (RCL)

Cuatro requisitos, iguales en todos los hostings:

1. **DI:** `services.AddKnowledgeHubBlazor(o => { ... })` (registra los servicios de Radzen).
2. **Página host** (index.html / App.razor): CSS de Radzen + CSS del módulo + JS de Radzen:

   ```html
   <link rel="stylesheet" href="_content/Radzen.Blazor/css/material.css" />
   <link rel="stylesheet" href="_content/MgSoftDev.KnowledgeHub.Blazor/knowledgehub.css" />
   ...
   <script src="_content/Radzen.Blazor/Radzen.Blazor.js"></script>
   ```

3. **Componente raíz:** `<RadzenComponents />` junto al `<Router>` (host de diálogos y
   notificaciones de Radzen).
4. **Router:** agrega el ensamblado de la RCL para que resuelvan las rutas `/kh/*`:

   ```razor
   <Router AppAssembly="@typeof(App).Assembly"
           AdditionalAssemblies="new[] { typeof(KnowledgeHubAssemblyMarker).Assembly }">
   ```

Rutas disponibles (clase `KnowledgeHubRoutes`): `/kh` (inicio), `/kh/page/{pk}`,
`/kh/edit/{pk}`, `/kh/history/{pk}`, `/kh/version/{pk}`, `/kh/permissions/{pk}`,
`/kh/manage/{pk}`, `/kh/search?q=`, `/kh/diagnostics`. Usa siempre los helpers
(`KnowledgeHubRoutes.Page(pk)`, etc.) en vez de literales.

Opciones de UI (`KnowledgeHubBlazorOptions`):

```csharp
services.AddKnowledgeHubBlazor(o =>
{
    o.PortalTitle = "📚 Documentación";                 // título del sidebar
    o.HeaderActionsComponent = typeof(MisLinksHost);    // TU componente en el pie del sidebar
    o.EditorTools.Add(...);                             // herramientas del editor (§8)
});
```

`HeaderActionsComponent` es el gancho para inyectar acciones del anfitrión en el sidebar del
módulo (link de administración, botón de cerrar sesión, etc.). Es un componente Blazor tuyo.

---

## 4. Storage: elegir y configurar el proveedor

### 4.1 LiteDB

Para anfitriones **sin base de datos** (persistencia en un archivo, cero servidor):

```csharp
services.AddKnowledgeHubLiteDbStore(
    Path.Combine(dataFolder, "knowledgehub.db"),
    o => o.CollectionPrefix = "kh_");      // opcional; default "kh_"
```

- Las colecciones e índices se crean solos al primer uso.
- ⚠️ **Single-process por diseño** (LiteDB en modo Direct). Para varios equipos editando a la
  vez usa SQL Server.
- ⚠️ **Un archivo `.db` exclusivo para el módulo.** Si tu app también usa LiteDB, usa OTRO
  archivo para tus datos: dos instancias `LiteDatabase` no pueden abrir el mismo archivo.

### 4.2 SQL Server

Para anfitriones con SQL Server; las tablas del módulo viven **dentro de tu base de datos**,
aisladas en un esquema (default `kh`) y/o con prefijo de tabla:

```csharp
services.AddKnowledgeHubSqlServerStore(connectionString, o =>
{
    o.Schema = "kh";        // null/"" → dbo
    o.TablePrefix = "";     // p. ej. "KH_" si no quieres esquema
});
```

**Crear las tablas** (la base de datos debe existir; la librería no la crea). Dos vías:

```csharp
// A) Al arrancar la app (idempotente, seguro repetir):
var result = await KnowledgeHubSqlSchema.EnsureDatabaseObjectsAsync(connectionString,
    new KnowledgeHubEfModelOptions { Schema = "kh" });
if (!result.Ok) { /* revisa el log */ }

// B) Exportar el script para tu pipeline de migraciones o para el DBA:
var script = KnowledgeHubSqlSchema.GetCreateScript(new KnowledgeHubEfModelOptions { Schema = "kh" });
File.WriteAllText("kh-schema.sql", script.Value);   // ejecútalo con SSMS/sqlcmd
```

Notas:
- Convenciones de las tablas: PK `Pk` (GUID), FKs `Fk_*`, auditoría `Row*` al final. La
  auditoría de usuario son columnas string (`RowUserCreate`/`RowUserUpdate`) — sin FK a
  ninguna tabla de usuarios.
- Los IDs y timestamps se generan **en el cliente**; los `DEFAULT` del DDL son solo red de
  seguridad para INSERTs manuales.
- Soporta edición concurrente multi-equipo: la publicación es transaccional con detección de
  conflicto optimista ("Otro usuario publicó una versión más reciente…").
- Connection string recomendada: incluye `TrustServerCertificate=True` en dev.

### 4.3 Implementar un store propio

Si ningún provider aplica (p. ej. quieres usar tu propia capa de persistencia), implementa
`IKnowledgeHubStore` (en Abstractions) y regístralo tú:

```csharp
services.AddScoped<IKnowledgeHubStore, MiStorePropio>();
services.AddKnowledgeHubCore(...);   // el core consume tu store
```

**Reglas del contrato** (imprescindibles — el core depende de ellas):

1. Los métodos "find" devuelven `Returning<T?>` **Ok con `Value = null`** cuando la fila no
   existe. Nunca lances excepción por "no encontrado".
   ⚠️ En C#, dentro de un lambda cuyo destino es `Returning<T>`, un `return null;` desnudo
   produce una **referencia Returning nula** (¡bug!). Escribe siempre el null tipado:
   `return (PageHeaderDto?)null;` — o usa un ternario tipado.
2. El filtro de visibilidad (`VisibilityFilter`) se aplica **DENTRO de la query**, nunca
   después de materializar. Visible = `IsPublic` **o** algún `DocPagePermission` activo cuyo
   `Permission` esté en `filter.Permissions` (case-insensitive). `SeesEverything` lo salta.
3. Las entidades llegan **completas** (Pk, timestamps, auditoría ya puestos por el core).
   El store solo escribe lo que recibe; usa el `AuditStamp` de los métodos que lo piden.
4. `TryPublishAsync(pagePk, baseVersionNumber, audit)` debe ser **atómico** en tu motor y
   devolver `PublishOutcome` (`Published | Conflict | PageNotFound | NothingToPublish`)
   siguiendo el algoritmo documentado en el XML-doc de la interfaz.
5. `InsertVersionAsync(version, pageImagePks)` es una unidad atómica: inserta la versión Y
   reemplaza los vínculos página↔imagen. Debe fallar si `(Fk_DocPage, VersionNumber)` ya existe.
6. `GetImageRefsAsync` devuelve solo `Pk + ContentHash` — **jamás materialices el binario**
   en esa consulta. `GetImageContentsAsync` trae todos los binarios pedidos en UN batch.
7. La búsqueda (`SearchPublishedAsync`) es case-insensitive (invariant); si tu motor no lo da
   nativo, filtra los candidatos en memoria.
8. No llames `SaveLog()` en el store — la frontera de servicio ya loguea.

**Valida tu store** con el guion de paridad (46 checks): copia el patrón de
`Tests\KnowledgeHub.ParityHarness` (registra tu store donde se registra el de LiteDB y ejecuta
`ParityScript.RunAsync`). Si pasa 46/46, tu store es correcto.

---

## 5. Implementación en WPF (BlazorWebView)

Referencia completa: `Demos\KnowledgeHub.Demo.Wpf`.

### Paso 1 — Proyecto

`.csproj` (SDK Razor, no el SDK normal, para poder compilar `.razor`):

```xml
<Project Sdk="Microsoft.NET.Sdk.Razor">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <!-- ⚠️ CRÍTICO: el TFM DEBE llevar la versión del SDK de Windows. Sin ella,
         Microsoft.Windows.SDK.NET no se copia y el BlazorWebView CRASHEA al mostrarse. -->
    <TargetFramework>net10.0-windows10.0.19041.0</TargetFramework>
    <UseWPF>true</UseWPF>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.AspNetCore.Components.WebView.Wpf" Version="10.0.80" />
    <PackageReference Include="MgSoftDev.KnowledgeHub" Version="0.1.0-preview.1" />
    <PackageReference Include="MgSoftDev.KnowledgeHub.Storage.LiteDb" Version="0.1.0-preview.1" />
    <PackageReference Include="MgSoftDev.KnowledgeHub.Blazor" Version="0.1.0-preview.1" />
    <PackageReference Include="Microsoft.Extensions.Hosting" Version="10.0.10" />
  </ItemGroup>
</Project>
```

### Paso 2 — Contexto de usuario (singleton mutable)

```csharp
using MgSoftDev.KnowledgeHub.Contracts;
using MgSoftDev.KnowledgeHub.Security;
using MgSoftDev.ReturningCore;

public sealed class AppUserContext : IKnowledgeHubUserContext
{
    private IReadOnlyList<string> _permissions = Array.Empty<string>();

    public bool IsAuthenticated { get; private set; }
    public string UserName { get; private set; } = string.Empty;
    public string DisplayName { get; private set; } = string.Empty;
    public IReadOnlyList<string> Permissions => _permissions;

    /// Llámalo tras TU login (o al arrancar si la app ya conoce al usuario, p. ej. Windows auth).
    public void SetUser(string userName, string displayName, IReadOnlyList<string> permissions)
    {
        UserName = userName;
        DisplayName = displayName;
        _permissions = permissions;
        IsAuthenticated = true;
    }

    public Task<ReturningList<PermissionInfo>> GetPermissionCatalogAsync() =>
        Task.FromResult(ReturningList<PermissionInfo>.Try(() => new List<PermissionInfo>
        {
            // TU catálogo: roles/permisos de tu sistema, proyectados a (Name, DisplayName).
            new("Role.Editor", "Editores"),
            new("Role.Produccion", "Producción")
        }));
}
```

### Paso 3 — DI en `App.xaml.cs`

```csharp
using MgSoftDev.KnowledgeHub;
using MgSoftDev.KnowledgeHub.Blazor;
using MgSoftDev.KnowledgeHub.Contracts;
using MgSoftDev.KnowledgeHub.Storage.LiteDb;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

public partial class App : Application
{
    private IHost? _host;
    public static IServiceProvider Services { get; private set; } = null!;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var dataFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MiApp");
        Directory.CreateDirectory(dataFolder);

        var builder = Host.CreateApplicationBuilder();

        // 1) Identidad del anfitrión.
        builder.Services.AddSingleton<AppUserContext>();
        builder.Services.AddSingleton<IKnowledgeHubUserContext>(sp => sp.GetRequiredService<AppUserContext>());

        // 2) KnowledgeHub: store + core + caché de disco + UI.
        builder.Services.AddKnowledgeHubLiteDbStore(Path.Combine(dataFolder, "knowledgehub.db"));
        builder.Services.AddKnowledgeHubCore(o => o.PublicAssetsBaseUrl = "https://docs-assets");
        builder.Services.AddKnowledgeHubFileImageCache(Path.Combine(dataFolder, "kh-cache"));
        builder.Services.AddKnowledgeHubBlazor(o => o.PortalTitle = "📚 Mi Documentación");

        // 3) Hosting WPF.
        builder.Services.AddWpfBlazorWebView();
#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
#endif
        builder.Services.AddTransient<MainWindow>();

        _host = builder.Build();
        Services = _host.Services;
        await _host.StartAsync();

        // 4) Autentica con TU mecanismo y setea el contexto ANTES de abrir la ventana.
        _host.Services.GetRequiredService<AppUserContext>()
            .SetUser("jgarcia", "Juan García", new[] { KnowledgeHubPermissions.Admin });

        MainWindow = _host.Services.GetRequiredService<MainWindow>();
        MainWindow.Show();
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_host is not null) { await _host.StopAsync(TimeSpan.FromSeconds(2)); _host.Dispose(); }
        base.OnExit(e);
    }
}
```

> Si tu app tiene ventana de login propia, sigue el patrón del demo (`LoginWindow.xaml.cs`):
> `App.xaml` con `ShutdownMode="OnExplicitShutdown"` y, tras mostrar `MainWindow`, cambiar a
> `ShutdownMode.OnMainWindowClose` — si no, cerrar el login apaga la app.

### Paso 4 — Componente raíz `Components\AppRoot.razor`

```razor
<RadzenComponents />

<Router AppAssembly="@typeof(AppRoot).Assembly"
        AdditionalAssemblies="new[] { typeof(KnowledgeHubAssemblyMarker).Assembly }">
    <Found Context="routeData">
        <RouteView RouteData="@routeData" DefaultLayout="@typeof(KnowledgeHubLayout)" />
    </Found>
    <NotFound>
        <LayoutView Layout="@typeof(KnowledgeHubLayout)">
            <div class="kh-doc-content"><h1>Página no encontrada</h1></div>
        </LayoutView>
    </NotFound>
</Router>
```

y una página de arranque `Components\Pages\RootRedirect.razor`:

```razor
@page "/"
@inject NavigationManager Nav
@code { protected override void OnInitialized() => Nav.NavigateTo(KnowledgeHubRoutes.Home); }
```

Con este `_Imports.razor` mínimo en el proyecto:

```razor
@using Microsoft.AspNetCore.Components
@using Microsoft.AspNetCore.Components.Routing
@using Microsoft.AspNetCore.Components.Web
@using Radzen
@using Radzen.Blazor
@using MgSoftDev.KnowledgeHub
@using MgSoftDev.KnowledgeHub.Blazor
@using MgSoftDev.KnowledgeHub.Blazor.Components.Layout
```

### Paso 5 — `MainWindow` con el BlazorWebView y el virtual host de imágenes

```csharp
using MgSoftDev.KnowledgeHub.Contracts;
using Microsoft.AspNetCore.Components.WebView;
using Microsoft.AspNetCore.Components.WebView.Wpf;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Web.WebView2.Core;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        var blazor = new BlazorWebView { HostPage = "wwwroot/index.html", Services = App.Services };
        blazor.RootComponents.Add(new RootComponent
        {
            Selector = "#app",
            ComponentType = typeof(Components.AppRoot)
        });
        blazor.BlazorWebViewInitialized += OnWebViewInitialized;
        RootHost.Children.Add(blazor);   // RootHost = un Grid en el XAML
    }

    // ⚠️ CRÍTICO: mapea el host virtual al caché de disco. El nombre "docs-assets" debe
    // corresponder con PublicAssetsBaseUrl = "https://docs-assets" del Paso 3.
    private void OnWebViewInitialized(object? sender, BlazorWebViewInitializedEventArgs e)
    {
        using var scope = App.Services.CreateScope();
        var cache = scope.ServiceProvider.GetRequiredService<IKnowledgeHubImageCache>();
        e.WebView.CoreWebView2.SetVirtualHostNameToFolderMapping(
            "docs-assets", cache.CacheFolder, CoreWebView2HostResourceAccessKind.Allow);
    }
}
```

### Paso 6 — `wwwroot\index.html`

```html
<!DOCTYPE html>
<html lang="es">
<head>
    <meta charset="utf-8" />
    <base href="/" />
    <link rel="stylesheet" href="_content/Radzen.Blazor/css/material.css" />
    <link rel="stylesheet" href="_content/MgSoftDev.KnowledgeHub.Blazor/knowledgehub.css" />
</head>
<body>
    <div id="app">Cargando…</div>
    <script src="_content/Radzen.Blazor/Radzen.Blazor.js"></script>
    <script src="_framework/blazor.webview.js"></script>
</body>
</html>
```

**Listo.** `dotnet run` y el portal abre en `/` → `/kh`.

---

## 6. Implementación en Blazor Server

Referencia completa: `Demos\KnowledgeHub.Demo.BlazorServer`. Aquí cada circuito es un usuario
distinto, por lo que el contexto de usuario es **Scoped** y lee la identidad de la cookie.

### Paso 1 — Paquetes

`KnowledgeHub`, `Storage.*`, `Blazor`, `AspNetCore` (proyecto SDK `Microsoft.NET.Sdk.Web`).

### Paso 2 — Contexto de usuario por circuito

```csharp
using System.Security.Claims;
using MgSoftDev.KnowledgeHub.Contracts;
using MgSoftDev.KnowledgeHub.Security;
using MgSoftDev.ReturningCore;
using Microsoft.AspNetCore.Components.Authorization;

public sealed class ServerUserContext : IKnowledgeHubUserContext
{
    private readonly AuthenticationStateProvider _authState;
    private ClaimsPrincipal? _principal;
    private IReadOnlyList<string>? _permissions;

    public ServerUserContext(AuthenticationStateProvider authState) => _authState = authState;

    // En Blazor Server el provider devuelve una tarea ya completada sembrada de la cookie.
    private ClaimsPrincipal Principal =>
        _principal ??= _authState.GetAuthenticationStateAsync().GetAwaiter().GetResult().User;

    public bool IsAuthenticated => Principal.Identity?.IsAuthenticated == true;
    public string UserName => Principal.Identity?.Name ?? string.Empty;
    public string DisplayName => Principal.FindFirst("FullName")?.Value ?? UserName;

    public IReadOnlyList<string> Permissions =>
        _permissions ??= IsAuthenticated
            ? MyAppPermissions.ForRoles(Principal.FindAll(ClaimTypes.Role).Select(c => c.Value))
            : Array.Empty<string>();

    public Task<ReturningList<PermissionInfo>> GetPermissionCatalogAsync() =>
        Task.FromResult(ReturningList<PermissionInfo>.Try(() => /* tu catálogo */ new List<PermissionInfo>()));
}
```

### Paso 3 — `Program.cs`

```csharp
var builder = WebApplication.CreateBuilder(args);

// Blazor Server + cookie auth.
builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(o => o.LoginPath = "/login");
builder.Services.AddAuthorization();

// Identidad del anfitrión (SCOPED: por circuito).
builder.Services.AddScoped<IKnowledgeHubUserContext, ServerUserContext>();

// KnowledgeHub.
builder.Services.AddKnowledgeHubLiteDbStore(Path.Combine(dataFolder, "knowledgehub.db"));
// (o AddKnowledgeHubSqlServerStore(connectionString) + EnsureDatabaseObjectsAsync)
builder.Services.AddKnowledgeHubCore(o => o.PublicAssetsBaseUrl = "/kh/assets");
builder.Services.AddKnowledgeHubFileImageCache(Path.Combine(dataFolder, "kh-cache"));
builder.Services.AddKnowledgeHubBlazor(o => o.PortalTitle = "📚 Mi Documentación");

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();
app.MapStaticAssets();

// ⚠️ El endpoint de imágenes — coincide con PublicAssetsBaseUrl.
app.MapKnowledgeHubAssets();

// Endpoints de sesión (la cookie se emite en un request HTTP normal, no en el circuito):
// POST /auth/login lee el form, valida contra TU sistema, hace SignInAsync con claims
// (Name, FullName, roles) y redirige a KnowledgeHubRoutes.Home.
// GET /auth/logout hace SignOutAsync. — copia ambos del demo, ~30 líneas.

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddAdditionalAssemblies(typeof(KnowledgeHubAssemblyMarker).Assembly);

app.Run();
```

### Paso 4 — `Components\App.razor` (⚠️ patrón de render mode condicional)

Para que una página de login **estática** pueda emitir la cookie, usa render mode por página —
NO un `@rendermode` fijo:

```razor
<!DOCTYPE html>
<html lang="es">
<head>
    <meta charset="utf-8" />
    <base href="/" />
    <link rel="stylesheet" href="_content/Radzen.Blazor/css/material.css" />
    <link rel="stylesheet" href="_content/MgSoftDev.KnowledgeHub.Blazor/knowledgehub.css" />
    <HeadOutlet @rendermode="@PageRenderMode" />
</head>
<body>
    <Routes @rendermode="@PageRenderMode" />
    <script src="_content/Radzen.Blazor/Radzen.Blazor.js"></script>
    <script src="_framework/blazor.web.js"></script>
</body>
</html>

@code {
    [CascadingParameter] private HttpContext HttpContext { get; set; } = default!;
    private IComponentRenderMode? PageRenderMode =>
        HttpContext.AcceptsInteractiveRouting() ? InteractiveServer : null;
}
```

y la página de login lleva `@attribute [ExcludeFromInteractiveRouting]` con un `<form
method="post" action="/auth/login">` clásico (copia `Components\Pages\Login.razor` del demo).

`Components\Routes.razor` = el mismo Router del §3.4 con `<RadzenComponents />`.

---

## 7. Implementación en Blazor WebAssembly + Web API

Referencia completa: `Demos\KnowledgeHub.Demo.Wasm` (+ `.Server`). Arquitectura: el **server**
corre el core + store y expone la API; el **cliente WASM** usa las implementaciones HTTP de
los contratos — los componentes de la RCL no notan la diferencia. Recomendado *hosted* (mismo
origen → sin CORS).

### 7.1 El server de la API

Paquetes: `KnowledgeHub`, `Storage.*`, `AspNetCore`, `Http.Server`,
`Microsoft.AspNetCore.Components.WebAssembly.Server` (+ referencia al proyecto cliente).

```csharp
var builder = WebApplication.CreateBuilder(args);

// Identidad por REQUEST: resuelve tu token/cookie/JWT → usuario + permisos.
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IKnowledgeHubUserContext, RequestUserContext>();

// KnowledgeHub in-process en el server.
builder.Services.AddKnowledgeHubLiteDbStore(Path.Combine(dataFolder, "knowledgehub.db"));
builder.Services.AddKnowledgeHubCore(o => o.PublicAssetsBaseUrl = "/kh/assets");
builder.Services.AddKnowledgeHubFileImageCache(Path.Combine(dataFolder, "kh-cache"));

var app = builder.Build();

app.UseBlazorFrameworkFiles();   // sirve el cliente WASM (hosted)
app.UseStaticFiles();

// Tus endpoints de sesión: POST /auth/login → valida y devuelve un token; POST /auth/logout.
// (El demo usa tokens opacos en memoria — ver Demos\KnowledgeHub.Demo.Wasm.Server\Auth\TokenAuth.cs.
//  En producción usa JWT o el mecanismo de tu sistema.)

// La API del módulo + imágenes. La auth la aplicas TÚ vía configureGroup si lo deseas:
app.MapKnowledgeHubApi("/kh/api" /*, g => g.RequireAuthorization() */);
app.MapKnowledgeHubAssets();

app.MapFallbackToFile("index.html");
app.Run();
```

`RequestUserContext` (scoped por request) lee el header `Authorization`, resuelve el usuario
con tu mecanismo y expone `UserName/Permissions` (mismo patrón del §3.1; ejemplo completo en
el demo). **Este contexto es la autoridad**: la API valida permisos en cada llamada aunque el
cliente mienta.

Contrato de la API que queda montada: un endpoint por método de servicio bajo `/kh/api/*`,
más `GET /kh/api/me` (identidad + permisos + catálogo, para que el cliente se hidrate) y
`POST /kh/api/html/prepare` (el rewriter de imágenes). Todos devuelven HTTP 200 con un payload
`ApiResult` — los avisos de negocio (p. ej. conflicto de publicación) viajan dentro, no como
errores HTTP.

### 7.2 El cliente WASM

Paquetes: `Http.Client`, `Blazor`, `Microsoft.AspNetCore.Components.WebAssembly`.

```csharp
var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// HttpClient con TU handler de auth (agrega el token a cada request).
builder.Services.AddSingleton<TokenHolder>();
builder.Services.AddSingleton(sp => new HttpClient(new BearerTokenHandler(sp.GetRequiredService<TokenHolder>()))
{
    BaseAddress = new Uri(builder.HostEnvironment.BaseAddress)
});

// Contexto de usuario del CLIENTE (solo para la UI), poblado desde /kh/api/me tras el login.
builder.Services.AddSingleton<ClientUserContext>();
builder.Services.AddSingleton<IKnowledgeHubUserContext>(sp => sp.GetRequiredService<ClientUserContext>());

// KnowledgeHub: contratos sobre HTTP + UI. ¡No hay store ni core en el cliente!
builder.Services.AddKnowledgeHubHttpClient();     // ApiBasePath default: "/kh/api"
builder.Services.AddKnowledgeHubBlazor(o => o.PortalTitle = "📚 Mi Documentación");

await builder.Build().RunAsync();
```

Piezas del cliente (todas con ejemplo en el demo, carpeta `Auth\` y `Pages\Login.razor`):

- `TokenHolder` + `BearerTokenHandler` (`DelegatingHandler` que agrega `Authorization: Bearer`).
- `ClientUserContext : IKnowledgeHubUserContext` con un método `RefreshAsync(http)` que hace
  `GET /kh/api/me` y guarda `MeResponse` (tipo en `MgSoftDev.KnowledgeHub.Transport`).
- Página de login que llama a tu `POST /auth/login`, guarda el token, refresca el contexto y
  navega a `KnowledgeHubRoutes.Home`.
- `App.razor` = el mismo Router del §3.4; `wwwroot/index.html` igual al de WPF pero con
  `blazor.webassembly.js`.

> Nota: si guardas el token solo en memoria, se pierde al refrescar la página (F5). Para
> persistencia usa `sessionStorage`/`localStorage` vía JS interop, con las consideraciones de
> seguridad de tu proyecto.

---

## 8. Herramientas personalizadas del editor HTML

El editor trae 4 callouts integrados (Nota, Advertencia, Importante, Personalizado…) que se
registran por el **mismo mecanismo** que las tuyas — puedes quitarlos, reemplazarlos o agregar
más, todo desde el anfitrión:

```csharp
services.AddKnowledgeHubBlazor(o =>
{
    // Quitar un built-in:
    o.EditorTools.RemoveAll(t => t.CommandName == "CalloutImportante");

    // Herramienta simple (inserta HTML directo):
    o.EditorTools.Add(new EditorToolDescriptor
    {
        CommandName = "FirmaAutor",                     // clave única
        Icon = "draw",                                  // ícono Material Symbols
        Title = "Insertar firma del autor",             // tooltip
        ExecuteAsync = ctx => Task.FromResult<string?>(
            $"<p><em>— {ctx.User.DisplayName}, {DateTime.Now:dd/MM/yyyy}</em></p>")
    });

    // Herramienta con diálogo (devuelve null si el usuario cancela → no inserta nada):
    o.EditorTools.Add(new EditorToolDescriptor
    {
        CommandName = "MiDialogo", Icon = "table_chart", Title = "Insertar tabla especial…",
        ExecuteAsync = async ctx =>
        {
            var result = await ctx.Dialog.OpenAsync<MiDialogoComponente>("Configura la tabla",
                parameters: null, new DialogOptions { Width = "540px" });
            return result is MiSpec spec ? ConstruirHtml(spec) : null;
        }
    });
});
```

`EditorToolContext` te da: `Dialog` (DialogService de Radzen), `Services` (service provider del
scope) y `User` (el contexto de usuario — puedes adaptar la herramienta a permisos).

Consejos para el HTML insertado:
- Usa **estilos inline** (el HTML viaja dentro del contenido y se ve igual en cualquier host).
- Si insertas un `<div>` estilizado, pon el texto en un `<p>` interno y agrega `<p><br></p>`
  al final — así Enter no clona la caja. `CalloutHtml.Build(bg, border, accent, icon, title,
  text)` es público y ya lo hace, por si quieres reusar el estilo de los callouts.

---

## 9. Seeding de contenido de ejemplo

Opcional. La librería trae un seeder de contenido demo (árbol de 5 páginas con imágenes
procedurales) — útil para validar la integración de inmediato:

```csharp
using var scope = app.Services.CreateScope();   // o _host.Services en WPF
var seeder = scope.ServiceProvider.GetRequiredService<KnowledgeHubContentSeeder>();
await seeder.SeedSampleContentIfEmptyAsync(new Dictionary<string, string[]>
{
    // Mapea las 3 páginas restringidas del demo a permisos de TU catálogo:
    ["documentacion-tecnica"] = new[] { "Role.Editor" },
    ["produccion"] = new[] { "Role.Produccion" },
    ["oficinas"] = new[] { "Role.Oficinas" }
});
```

Solo siembra si no hay ninguna página (idempotente). Los **usuarios** los siembras tú — son
del anfitrión.

---

## 10. Permisos finos (opt-in)

Por defecto (paridad con el diseño original), quien tiene `KnowledgeHub.Edit` también publica
y gestiona visibilidad. Si quieres separar capacidades:

```csharp
services.AddKnowledgeHubCore(o =>
{
    o.PublicAssetsBaseUrl = "...";
    o.UseFineGrainedPublish = true;             // publicar exige KnowledgeHub.Publish (o Admin)
    o.UseFineGrainedManagePermissions = true;   // visibilidad exige KnowledgeHub.ManagePermissions (o Admin)
});
```

Recuerda entonces incluir esos permisos en `Permissions` de los usuarios que correspondan.
Otra opción de `KnowledgeHubOptions`: `MaxImageWidth` (default 1600 px — las imágenes más
anchas se reducen al ingresarlas).

---

## 11. Solución de problemas (gotchas)

| Síntoma | Causa | Solución |
|---|---|---|
| WPF crashea al mostrar la ventana (`WebView2CompositionControl.TryInitializeD3DImage`) | TFM sin versión de Windows SDK | `<TargetFramework>net10.0-windows10.0.19041.0</TargetFramework>` |
| Las imágenes no cargan en WPF | Virtual host no mapeado o nombre distinto | `SetVirtualHostNameToFolderMapping("docs-assets", cache.CacheFolder, ...)` y `PublicAssetsBaseUrl = "https://docs-assets"` deben coincidir |
| Las imágenes no cargan en Server/WASM | Falta el endpoint o el patrón no coincide | `app.MapKnowledgeHubAssets()` con el mismo path que `PublicAssetsBaseUrl` |
| Rutas `/kh/*` dan "not found" | El Router no escanea la RCL | `AdditionalAssemblies = new[] { typeof(KnowledgeHubAssemblyMarker).Assembly }` |
| La UI se ve sin estilos / los diálogos no abren | Falta CSS/JS de Radzen o `<RadzenComponents />` | Revisa los 4 requisitos del §3.4 |
| La página de login estática no aparece en Blazor Server | `@rendermode` fijo en `<Routes>` | Patrón `HttpContext.AcceptsInteractiveRouting()` (§6 paso 4) + `[ExcludeFromInteractiveRouting]` |
| «No tienes permiso…» siendo admin | El usuario no trae los permisos reservados | Incluye `KnowledgeHubPermissions.Admin` / `.Edit` en `Permissions` |
| LiteDB: "file is locked / already open" | Dos `LiteDatabase` sobre el mismo archivo | Archivo `.db` exclusivo para el módulo (§4.1) |
| Varios equipos pisándose con LiteDB | LiteDB es single-process | Usa el provider SqlServer para multi-equipo |
| (Store propio) NullReferenceException al consultar algo inexistente | `return null;` desnudo en lambda `Returning<T>` | Null **tipado**: `return (MiDto?)null;` (§4.3 regla 1) |
| (Store propio) Un usuario ve páginas ajenas | Filtro de visibilidad aplicado después de materializar | Filtra DENTRO de la query (§4.3 regla 2) |
| El token WASM se pierde al refrescar | Token solo en memoria | Persistir en `sessionStorage` (nota del §7.2) |
| Los servicios "recuerdan" al usuario anterior (Server) | Contexto de usuario registrado singleton | En Blazor Server el user context va **Scoped** (§3.1) |

---

## 12. Checklist final de verificación

Al terminar la integración, verifica en la app corriendo:

- [ ] **Login/identidad**: el sidebar muestra el `DisplayName`; un usuario sin permisos
      reservados no ve los botones de crear/editar.
- [ ] **Visibilidad**: dos usuarios con permisos distintos ven árboles distintos; una página
      no pública solo la ven los permisos asignados; los hijos de una página oculta no aparecen.
- [ ] **Edición**: crear página → editar → guardar borrador → publicar → el lector la muestra.
- [ ] **Conflicto**: con dos sesiones sobre la misma página, publicar en B y luego en A →
      A recibe el aviso «Otro usuario publicó una versión más reciente…» y no pisa a B.
- [ ] **Imágenes**: pega una imagen en el editor y guarda → se ve al leer; en el HTML
      almacenado queda `docimg://` (nunca URLs de display); la segunda visita sirve la imagen
      desde caché (disco en WPF/Server, navegador en WASM).
- [ ] **Historial**: la lista muestra autor (`UserName`) y versión publicada marcada;
      restaurar crea una versión nueva sin borrar historial.
- [ ] **Herramientas del editor**: los callouts (y las tuyas) insertan HTML y Enter no clona
      la caja.
- [ ] **Diagnóstico** (`/kh/diagnostics`): muestra métricas tras navegar; hits de caché
      crecen en visitas repetidas.

---

*Guía generada para MgSoftDev.KnowledgeHub v0.1.0-preview.1 (.NET 10). Los demos de `Demos\`
compilan con 0 warnings y están verificados end-to-end; úsalos como referencia canónica.*
