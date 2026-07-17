# MgSoftDev.KnowledgeHub.AspNetCore

Helpers de hosting ASP.NET Core para KnowledgeHub: el endpoint de assets de imágenes.

```csharp
app.MapKnowledgeHubAssets();   // GET /kh/assets/{hash}.webp
```

Sirve los binarios WebP direccionados por su hash de contenido con
`Cache-Control: public, max-age=31536000, immutable` — tras la primera carga manda el caché
del navegador. Con `AddKnowledgeHubFileImageCache` registrado trabaja cache-aside contra el
disco del servidor. El patrón debe coincidir con `KnowledgeHubOptions.PublicAssetsBaseUrl`.

Úsalo en anfitriones Blazor Server; el paquete `Http.Server` lo complementa cuando además
expones la API para clientes WASM.
