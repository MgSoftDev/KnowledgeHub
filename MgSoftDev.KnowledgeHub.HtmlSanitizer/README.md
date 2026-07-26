# MgSoftDev.KnowledgeHub.HtmlSanitizer

Limpieza de HTML por defecto para **MgSoftDev.KnowledgeHub**, sobre la librería
[HtmlSanitizer](https://github.com/mganss/HtmlSanitizer) (Ganss.Xss).

Quita la basura que mete Word al pegar (`<o:p>`, `class="MsoNormal"`, `<v:shape>`,
comentarios condicionales `<!--[if …]>`), scripts y manejadores de eventos, **conservando** las
dos formas de imagen que usa KnowledgeHub.

## Registro

```csharp
services.AddKnowledgeHubHtmlSanitizer();
```

Regístralo en **cada contenedor** que lo necesite: el de la UI limpia al pegar, el que ejecuta el
core limpia al guardar. En WASM eso significa cliente **y** servidor de la API.

Es opcional: sin él, la librería funciona igual que antes y no se limpia nada.

## Qué añade sobre la configuración de fábrica

Tres ajustes que **no** son opcionales para contenido de KnowledgeHub:

| Ajuste | Por qué |
|---|---|
| `AllowedSchemes.Add("data")` | Las imágenes pegadas viajan como `data:` hasta que se guardan; sin esto se borrarían antes de poder subirse. |
| `AllowedSchemes.Add("docimg")` | `docimg://{pk}` es la referencia almacenada; sin esto, limpiar al guardar borraría todas las imágenes existentes. |
| `AllowedCssProperties.Add("zoom")` | `zoom` no es estándar y no está en la lista de fábrica, pero es lo que escribe la herramienta de tamaño de imagen. |

## Ampliar las reglas

```csharp
services.AddKnowledgeHubHtmlSanitizer(o =>
{
    o.AllowedTags.Add("iframe");          // por ejemplo, para incrustar vídeo
    o.AllowedAttributes.Add("class");     // NO permitido de fábrica
    o.AllowedCssProperties.Add("filter");
});
```

Parte de `KnowledgeHubSanitizerDefaults.CreateSanitizer()`, así que los tres ajustes de arriba ya
están puestos.

## Sustituirlo por completo

Implementa `IKnowledgeHubHtmlSanitizer` (en `MgSoftDev.KnowledgeHub.Abstractions`) y regístralo
**antes** de llamar a `AddKnowledgeHubHtmlSanitizer` — o en lugar de llamarlo. El método usa
`TryAddSingleton`, así que la implementación del anfitrión gana.

El contexto (`Paste`, `Save`, `Manual`) permite ser más estricto al pegar que al guardar.

## Nota sobre la versión

Se usa la línea `9.1.x-beta` de HtmlSanitizer a propósito: es la primera que depende de
**AngleSharp ≥ 1.5.0**. Las versiones estables anteriores fijan AngleSharp `0.17.1`, afectado por
**CVE-2026-54570**, un fallo de parseo que permite **evadir sanitizadores** — justo lo que este
paquete debe impedir.

Documentación completa: [GUIA-IMPLEMENTACION.md](https://github.com/MgSoftDev/KnowledgeHub/blob/main/GUIA-IMPLEMENTACION.md)
