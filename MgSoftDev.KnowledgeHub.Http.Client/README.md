# MgSoftDev.KnowledgeHub.Http.Client

Implementaciones `HttpClient` de los contratos de servicio de KnowledgeHub, para **Blazor
WebAssembly** (o cualquier cliente remoto) contra un servidor que mapea `MapKnowledgeHubApi`.
Los componentes del paquete Blazor funcionan sin cambios encima de estas implementaciones.

```csharp
// WASM Program.cs
builder.Services.AddSingleton(sp => new HttpClient(/* tu handler de auth */)
{
    BaseAddress = new Uri(builder.HostEnvironment.BaseAddress)
});
builder.Services.AddKnowledgeHubHttpClient();          // servicios HTTP
builder.Services.AddKnowledgeHubBlazor();              // UI
builder.Services.AddSingleton<IKnowledgeHubUserContext, MiClientUserContext>();
// Pobla MiClientUserContext desde GET /kh/api/me tras autenticarte.
```

La semántica `Returning` se preserva a través del cable: los avisos de negocio del servidor
(p. ej. «Otro usuario publicó una versión más reciente…») llegan como `Unfinished` y las
notificaciones Radzen los muestran igual que en los hostings in-process.
