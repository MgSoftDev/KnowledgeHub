# MgSoftDev.KnowledgeHub.Pdf

Exportación a PDF para **MgSoftDev.KnowledgeHub**, impresa por **Chromium** a través de
[Playwright](https://playwright.dev/dotnet/).

Convierte una página publicada —o una rama entera, como un manual— en un PDF con portada, índice y
marcadores navegables. Como lo imprime un navegador de verdad, **sale exactamente como se ve en el
lector, con tu CSS incluido**, y no hay ningún mapeador HTML que mantener: lo que produzca el editor
mañana se renderiza solo.

## Registro

```csharp
services.AddKnowledgeHubPdf();
```

Regístralo **donde corre el core**, el mismo contenedor que `AddKnowledgeHubCore`. En WPF y Blazor
Server es el mismo; en WASM va en el **servidor de la API**, no en el cliente. Sin registrarlo, el
botón de exportar no aparece.

> El paquete arrastra `Microsoft.Playwright`: **195 MB de nupkg**, una vez por máquina en la caché
> de NuGet.

## De dónde sale el navegador

```csharp
services.AddKnowledgeHubPdf(o => o.BrowserSource = PdfBrowserSource.Auto);
```

| Modo | Qué hace | En tu app |
|---|---|---|
| `Auto` (defecto) | Edge instalado → copia empaquetada → caché. Recuerda el que funcionó. | según el que use |
| `SystemBrowser` | El **Edge ya instalado**. No descarga nada. | ~100 MB (driver) |
| `BundledWithApp` | Dentro de la carpeta de la app; entra en el MSI. **Sin internet.** | ~370 MB |
| `PlaywrightCache` | `%LOCALAPPDATA%\ms-playwright`, tras `playwright install`. | ~100 MB |
| `DownloadOnDemand` | Lo instala en la primera exportación. | ~100 MB |

Usar Edge te ahorra los 265 MB del navegador, pero **no** los ~100 MB del driver de Playwright. El
paquete trae un `.props` que fija `PlaywrightPlatform` a tu sistema: sin él serían **548 MB**, porque
se copiaría el driver de las cinco plataformas.

Si no hay ningún navegador, la exportación se rechaza con un mensaje que dice cuál falta — nunca una
excepción cruda.

## Temas, portada, cabecera y pie

```csharp
services.AddKnowledgeHubPdf(o =>
{
    // Se lee en CADA exportación: una empresa cambia su tema dejando el archivo junto al
    // ejecutable, sin recompilar.
    o.CssFilePath = Path.Combine(AppContext.BaseDirectory, "pdf-tema.css");
    o.LogoFilePath = Path.Combine(AppContext.BaseDirectory, "logo.png");

    o.Cover = new PdfTemplate { Html = "{{Logo}}<h1>{{Title}}</h1><div>{{GeneratedAt}}</div>" };
    o.Footer = new PdfTemplate { Html = "…<span class=\"pageNumber\"></span>…" };
});
```

Portada, cabecera y pie admiten `Html` (plantilla con `{{Title}}`, `{{GeneratedAt}}`,
`{{GeneratedBy}}`, `{{SectionCount}}`, `{{Logo}}`, `{{LogoSrc}}`), `FilePath` o `Factory`.

**Cabecera y pie se renderizan en un contexto aislado**: no les llega el CSS del documento, las
imágenes solo cargan como data URI (`{{LogoSrc}}`) y los estilos deben ir en línea. Si el margen es
menor que la cabecera, esta se superpone al contenido sin avisar: deja ~22 mm si lleva imagen.

## Marcadores

`EmbedOutline` (activado) mete el esquema navegable. **Activa también `TaggedPdf`**, porque Chromium
construye el esquema desde la estructura etiquetada y por su cuenta la opción se ignora en silencio.
Cuesta ~19% más de tamaño.

El índice lleva **enlaces internos pero no números de página**: Chromium no sabe dónde cae cada
sección hasta maquetar, y no hay segunda pasada.

## Memoria

El navegador **no queda residente**: se abre, imprime y se cierra en cada exportación, con las
llamadas serializadas y un tope de tiempo (`TimeoutSeconds`, 60 s). Exportar es ocasional y una app
de escritorio no debería sostener 200-300 MB por un botón que quizá nadie pulse.

Ver la [guía §8.2](https://github.com/MgSoftDev/KnowledgeHub/blob/main/GUIA-IMPLEMENTACION.md) para
el permiso de descarga, el MSBuild que empaqueta el navegador y los ejemplos completos.
