# MgSoftDev.KnowledgeHub.Blazor

UI Blazor del módulo KnowledgeHub (Razor Class Library): árbol de navegación, lector, editor
HTML (Radzen) con herramientas personalizadas inyectables, historial, permisos y diagnóstico.
Corre igual en **WPF BlazorWebView**, **Blazor Server** y **Blazor WebAssembly** — solo
depende de los contratos de Abstractions.

## Requisitos del anfitrión

1. **DI**: `services.AddKnowledgeHubBlazor(o => { ... })` (registra Radzen).
2. **Host page**: tema Radzen + CSS del módulo:
   ```html
   <link rel="stylesheet" href="_content/Radzen.Blazor/css/material.css" />
   <link rel="stylesheet" href="_content/MgSoftDev.KnowledgeHub.Blazor/knowledgehub.css" />
   <script src="_content/Radzen.Blazor/Radzen.Blazor.js"></script>
   ```
3. **Root component**: `<RadzenComponents />` junto al Router.
4. **Router**: agrega el ensamblado de la RCL:
   ```razor
   <Router AppAssembly="..." AdditionalAssemblies="new[] { typeof(KnowledgeHubAssemblyMarker).Assembly }">
   ```

Las páginas viven bajo el prefijo fijo **`/kh`** (`KnowledgeHubRoutes`).

## Herramientas del editor

Los 4 callouts integrados se registran por el mismo mecanismo que las tuyas — puedes
quitarlos o agregar más:

```csharp
services.AddKnowledgeHubBlazor(o =>
{
    o.PortalTitle = "📚 Documentación";
    o.HeaderActionsComponent = typeof(MisLinks);   // componente del anfitrión en el sidebar
    o.EditorTools.Add(new EditorToolDescriptor
    {
        CommandName = "MiTool", Icon = "star", Title = "Mi herramienta",
        ExecuteAsync = async ctx => "<p>HTML a insertar (o null si se cancela)</p>"
    });
});
```
