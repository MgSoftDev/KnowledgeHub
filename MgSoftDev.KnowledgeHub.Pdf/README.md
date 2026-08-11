# MgSoftDev.KnowledgeHub.Pdf

Exportación a PDF por defecto para **MgSoftDev.KnowledgeHub**, sobre
[PDFsharp/MigraDoc](https://www.pdfsharp.net/) (MIT).

Convierte una página publicada —o una rama entera, como un manual— en un PDF con portada, índice
con números de página reales, numeración continua y marcadores navegables. Totalmente gestionado:
sin navegador, sin binarios nativos y sin nada que instalar aparte.

## Registro

```csharp
services.AddKnowledgeHubPdf();
```

Regístralo **donde corre el core**, el mismo contenedor que `AddKnowledgeHubCore`. En WPF y Blazor
Server es el mismo; en WASM va en el **servidor de la API**, no en el cliente. Sin registrarlo, el
botón de exportar no aparece y la librería se comporta como antes.

Es opcional y sustituible: quien quiera fidelidad píxel-perfect implementa
`IKnowledgeHubPdfRenderer` con Playwright o el motor que prefiera, y **hereda intacto** el filtrado
de permisos, que vive en el core.

## Opciones

```csharp
services.AddKnowledgeHubPdf(o =>
{
    o.FontFamily = "Segoe UI";
    o.FontSize = 10;
    o.IncludeCover = true;
    o.IncludeTableOfContents = true;
    o.IncludePageNumbers = true;
    o.ConfigureFonts = true;      // false si configuras GlobalFontSettings tú
});
```

## Fuentes fuera de Windows

PDFsharp resuelve fuentes por `GlobalFontSettings`, que es **estático de proceso**. El paquete
activa las fuentes del sistema bajo Windows y solo escribe el resolver **si nadie lo ha hecho ya**,
para no pisar la configuración del anfitrión.

En Linux o en contenedores sin fuentes instaladas, generar el PDF **lanza una excepción con un
mensaje claro** en vez de producir un documento con cuadraditos: instala fuentes en la imagen, o
configura `GlobalFontSettings.FontResolver` y pon `ConfigureFonts = false`.

## Qué se traduce del HTML

Encabezados, párrafos, negrita/cursiva/subrayado, listas, enlaces, imágenes, tablas, citas, bloques
de código y los avisos de KnowledgeHub. **Una etiqueta que no se reconoce nunca se descarta: se
emite su texto**, porque perder contenido en silencio es peor que renderizarlo plano.

Las imágenes se almacenan en WebP, que PDFsharp no importa —y no avisa: produciría el PDF sin
ellas—, así que se transcodifican a PNG. Si una no se puede decodificar, aparece un marcador visible
en el documento.

Ver la [guía §8.2](https://github.com/MgSoftDev/KnowledgeHub/blob/main/GUIA-IMPLEMENTACION.md) para
el permiso de descarga, el tope de páginas y la receta de Playwright.
