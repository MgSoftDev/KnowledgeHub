# MgSoftDev.KnowledgeHub.Http.Server

Expone los contratos de servicio de KnowledgeHub como **minimal API** para clientes remotos
(Blazor WebAssembly con `MgSoftDev.KnowledgeHub.Http.Client`).

```csharp
app.MapKnowledgeHubApi("/kh/api", g => g.RequireAuthorization());  // auth = del anfitrión
app.MapKnowledgeHubAssets();                                        // imágenes (paquete AspNetCore)
```

- Todo endpoint devuelve HTTP 200 con un payload `ApiResult` cuando el pipeline funcionó:
  los resultados de negocio (incluido el conflicto de publicación) viajan DENTRO del payload
  como `Unfinished`, nunca como errores HTTP.
- La autenticación es del anfitrión (cookies, tokens, lo que uses); además los servicios
  validan permisos server-side contra `IKnowledgeHubUserContext` (regístralo scoped por
  request, resolviendo tu usuario desde claims/token).
- `GET /kh/api/me` permite al cliente remoto poblar su contexto de usuario local
  (identidad + permisos + catálogo).
