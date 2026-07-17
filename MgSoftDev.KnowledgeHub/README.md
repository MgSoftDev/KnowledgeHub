# MgSoftDev.KnowledgeHub

Servicios core del módulo de documentación colaborativa **KnowledgeHub**: árbol de páginas
con visibilidad por permisos y herencia, versionado insert-only, publicación atómica con
detección de conflicto, ingesta de imágenes WebP con deduplicación por SHA-256 y caché local
direccionado por hash.

## Registro mínimo

```csharp
services.AddKnowledgeHubLiteDbStore(dbPath);            // o AddKnowledgeHubSqlServerStore(cs)
services.AddKnowledgeHubCore(o => o.PublicAssetsBaseUrl = "/kh/assets");
services.AddKnowledgeHubFileImageCache(cacheFolder);    // opcional (hosts con disco local)
services.AddSingleton<IKnowledgeHubUserContext, MiUserContext>();  // TU implementación
```

`PublicAssetsBaseUrl` por hosting:

| Hosting | Valor | Quién sirve la imagen |
|---|---|---|
| WPF (BlazorWebView) | `https://docs-assets` | Virtual host de WebView2 mapeado a la carpeta de caché |
| Blazor Server | `/kh/assets` | `MapKnowledgeHubAssets()` del paquete AspNetCore |
| WASM (remoto) | URL del endpoint del server | El server de la API |

Los servicios son **Scoped** (WPF: el scope raíz vive toda la app; Blazor Server: un scope
por circuito/usuario). Todos los métodos devuelven `Returning`/`Returning<T>` (MgSoftDev.Returning).
