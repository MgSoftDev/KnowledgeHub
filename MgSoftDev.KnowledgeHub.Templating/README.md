# MgSoftDev.KnowledgeHub.Templating

Datos vivos en las páginas de **MgSoftDev.KnowledgeHub**, sobre
[Scriban](https://github.com/scriban/scriban).

Para la documentación que **miente porque nadie se acuerda de actualizarla**: la lista de roles del
sistema, los equipos de planta con su IP y su PLC, bloques que solo debe ver quien tenga cierto rol.
La página se rellena sola al abrirse.

## Registro

```csharp
services.AddKnowledgeHubTemplating();
```

Regístralo **donde corre el core**, el mismo contenedor que `AddKnowledgeHubCore`. En WASM va en el
**servidor de la API**: el navegador recibe la página ya renderizada y Scriban nunca se compila a
WebAssembly. Sin registrarlo, las páginas marcadas muestran sus llaves tal cual.

> Scriban es **BSD-2** y en net8.0+ entra con **cero dependencias transitivas**.

## Lo que hay sin escribir una línea

| Expresión | Qué da |
|---|---|
| `{{ kh.roles }}` | El catálogo de roles que tu app **ya** le pasa a la librería |
| `{{ kh.user.has "Role.X" }}` | Si quien lee tiene ese permiso |
| `{{ kh.user.name }}` · `.display_name` · `.permissions` | Quién está leyendo |
| `{{ kh.page.title }}` · `.slug` · `.pk` | La página |
| `{{ kh.is_pdf }}` | **Verdadero solo al exportar** |

```html
<ul>{{ for r in kh.roles }}<li>{{ r.display_name }}</li>{{ end }}</ul>

{{ if !kh.is_pdf }}<p>Esto no sale en el manual impreso.</p>{{ end }}
```

## Tus propios datos

```csharp
public sealed class EquiposProvider : IKnowledgeHubTemplateModelProvider
{
    public string Name => "equipos";                  // {{ for e in equipos }}

    public TemplateModelInfoDto Describe() => new() { Name = Name, Description = "Equipos de planta" };

    public async Task<object?> GetModelAsync(TemplateModelContext ctx) =>
        await _db.Equipos
            .Select(e => new { e.Linea, e.Pc, e.Ip, e.Plc })   // proyecta: NO devuelvas la entidad
            .ToListAsync(ctx.CancellationToken);
}

services.AddScoped<IKnowledgeHubTemplateModelProvider, EquiposProvider>();
```

> ⚠️ **No devuelvas objetos ricos.** Una plantilla puede leer **todas** las propiedades públicas de
> lo que le entregues, recursivamente: una entidad de EF arrastra sus navegaciones, y un `DbContext`
> o un `HttpContext` exponen muchísimo más de lo que pretendías.

Se llama en **cada vista** de una página que lo use — de eso va la feature. Si tu fuente es cara,
cachea **dentro de tu proveedor**, que es donde se conoce el coste.

## Opt-in por página, y con permiso propio

Una página solo se procesa si está marcada en **Gestionar página**, y marcarla exige
**`KnowledgeHub.Templates`** — el único permiso de la librería **sin modo grueso**: tener `Edit` no
basta. Escribir una plantilla recorre datos del anfitrión y ejecuta bucles en el servidor, en cada
visita, para cada lector.

Lo demás se queda intacto: una página que documente Angular o Handlebars conserva sus llaves.

## Seguridad y límites

- La salida renderizada **se sanea** con el `IKnowledgeHubHtmlSanitizer` registrado. Scriban no
  escapa nada, así que sin saneador un dato con `<script>` entraría crudo.
- Contexto endurecido: sin `include` ni `object.eval`, `LoopLimit` 500, `RecursiveLimit` 20, tope de
  salida y **timeout de 2 s** por render. Todo configurable.
- Publicar una plantilla rota **se rechaza** con la línea; guardar el borrador solo avisa.
- Un proveedor que falle cuesta su variable, nunca la página.

## Limitaciones

- **En atributos no funciona** (`class="{{ x }}"`): el saneador se lo lleva. Ponlo en el contenido.
- **La búsqueda indexa la plantilla, no el resultado.**
- **El historial no renderiza**, a propósito: es la herramienta de auditoría.

Ver la [guía §10.1](https://github.com/MgSoftDev/KnowledgeHub/blob/main/GUIA-IMPLEMENTACION.md)
para el detalle completo.
