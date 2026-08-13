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

## Seguridad: el hash es la credencial

Este endpoint **no autentica**, y no puede comprobar visibilidad: un binario no sabe a qué página
pertenece —la misma imagen puede estar en varias— y la deduplicación por contenido lo hace explícito.
Lo que lo protege es el **hash SHA-256**: 256 bits que solo se obtienen teniendo ya el HTML de una
página, que sí está filtrado por permisos. Quien tenga la URL, ve la imagen.

Si eso no encaja con tu caso, el método devuelve el `IEndpointConventionBuilder`, así que puedes
exigir autenticación:

```csharp
app.MapKnowledgeHubAssets().RequireAuthorization();
```

Eso lo cierra a usuarios autenticados, nunca por página. Ten en cuenta que una URL de imagen
compartida por correo, o incrustada en un PDF exportado, seguirá funcionando para quien la reciba.
