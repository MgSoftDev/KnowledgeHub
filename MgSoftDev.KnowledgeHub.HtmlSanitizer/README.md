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

> ⚠️ **En WASM hay dos contenedores y es fácil configurar solo uno.** El editor limpia en el
> cliente y el guardado ocurre en el servidor. Si configuras únicamente el cliente, todo se ve
> perfecto en pantalla y el servidor —aún con las reglas de fábrica— recorta el HTML camino de la
> base de datos. Falla al guardar, lejos de donde tocaste nada. Declara las opciones **una vez** y
> pásalas a los dos (ver abajo).
>
> El método usa `TryAddSingleton`, así que una llamada **sin configurar hecha antes** gana sobre la
> tuya y tu configuración se pierde en silencio. Regístralo una sola vez por contenedor.

Es opcional: sin él, la librería funciona igual que antes y no se limpia nada.

## Qué añade sobre la configuración de fábrica

Cuatro ajustes que **no** son opcionales para contenido de KnowledgeHub:

| Ajuste | Por qué |
|---|---|
| `AllowedSchemes.Add("data")` | Las imágenes pegadas viajan como `data:` hasta que se guardan; sin esto se borrarían antes de poder subirse. |
| `AllowedSchemes.Add("docimg")` | `docimg://{pk}` es la referencia almacenada; sin esto, limpiar al guardar borraría todas las imágenes existentes. |
| `AllowedCssProperties.Add("zoom")` | `zoom` no es estándar y no está en la lista de fábrica, pero es lo que escribe la herramienta de tamaño de imagen. |
| `AllowedAttributes.Add("class")` + `AllowedClasses.Add("kh-callout")` | Marca los avisos para que el nivel 2 les respete el color. Al sembrar `AllowedClasses` el filtro queda **activo**, así que de fábrica **solo sobrevive `kh-callout`** y el resto de clases se borra (incluidas las `MsoNormal` de Word). |

## Conservar tus propias clases CSS

Si maquetas con clases tuyas dentro de la documentación, **decláralas** o el guardado se las lleva:

```csharp
public static class MiSaneador
{
    public static KnowledgeHubSanitizerOptions Options { get; } = new()
    {
        AllowedClasses = { "mi-indice" },              // por nombre
        AllowedClassPrefixes = { "mi-tarjeta-" },      // una familia entera, presente y futura
        ConfigureStandard = s => s.AllowCssCustomProperties = true
    };
}

// en TODOS los contenedores que limpien (en WASM: cliente y servidor)
services.AddKnowledgeHubHtmlSanitizer(MiSaneador.Options);
```

- **Prefiere los prefijos** cuando la familia crezca con el diseño: una clase que nadie se acordó de
  registrar es indistinguible de basura pegada, y se pierde al guardar.
- Declarar clases **no** reabre la puerta a Word: la lista es de **inclusión**, así que `MsoNormal`
  se sigue borrando por no estar en ella.
- Llegan a los **niveles 1 y 2**. El nivel 2 salva la clase pero **no** la cosmética en línea
  (colores, fuentes): tu aspecto debe venir de tu CSS. El **nivel 3 quita todas las clases** a
  propósito —es «solo texto»—; si lo necesitas distinto, usa `ConfigureLevel`.

## Ampliar las reglas

```csharp
services.AddKnowledgeHubHtmlSanitizer(o =>
{
    o.AllowedTags.Add("iframe");          // por ejemplo, para incrustar vídeo
    o.AllowedCssProperties.Add("filter");
});
```

Parte de `KnowledgeHubSanitizerDefaults.CreateSanitizer()`, así que los ajustes de arriba ya están
puestos.

Ojo: esta sobrecarga **solo configura el nivel 1** (el que usa guardar). Los niveles 2 y 3 se
derivan de los defaults, para que ampliar uno no amplíe todos. Para llegar a los tres, usa
`KnowledgeHubSanitizerOptions` como en el apartado anterior.

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
