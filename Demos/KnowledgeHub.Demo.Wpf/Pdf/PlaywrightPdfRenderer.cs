using System.IO;
using System.Net;
using System.Text;
using MgSoftDev.KnowledgeHub;
using MgSoftDev.KnowledgeHub.Contracts;
using MgSoftDev.KnowledgeHub.Dtos;
using MgSoftDev.ReturningCore;
using MgSoftDev.ReturningCore.Helper;
using Microsoft.Playwright;

namespace KnowledgeHub.Demo.Wpf.Pdf;

/// <summary>
/// EJEMPLO de motor alternativo: en vez de PDFsharp, imprime con Chromium a través de Playwright.
/// El PDF sale idéntico a lo que se ve en pantalla y no hay ningún mapeador HTML que mantener.
///
/// Es lo ÚNICO que hay que escribir para cambiar de motor: los permisos, el filtrado por
/// visibilidad y la carga de imágenes siguen en el core, en IKnowledgeHubPdfExportService.
///
/// Pensado para instalación en planta SIN internet: el navegador viaja dentro de la carpeta de la
/// aplicación (ver el bloque KhBundleChromium del csproj), no en la caché del usuario.
/// </summary>
public sealed class PlaywrightPdfRenderer : IKnowledgeHubPdfRenderer, IAsyncDisposable
{
    /// <summary>
    /// El binario ligero: 265 MB frente a los 412 MB del Chromium completo, y suficiente para
    /// imprimir. Hay que instalarlo con ese mismo nombre (<c>playwright install
    /// chromium-headless-shell</c>).
    /// </summary>
    private const string HeadlessShellChannel = "chromium-headless-shell";

    // El contrato exige que el renderer sea seguro como singleton, y arrancar Chromium cuesta uno o
    // dos segundos: se abre UNA vez y se reutiliza. El semáforo protege esa inicialización perezosa
    // de dos exportaciones simultáneas.
    private readonly SemaphoreSlim _gate = new(1, 1);
    private IPlaywright? _playwright;
    private IBrowser? _browser;
    private bool _disposed;

    public async Task<Returning<byte[]>> RenderAsync(PdfExportDocument document)
    {
        if (document.Sections.Count == 0)
            return Returning.Unfinished("No hay contenido que exportar", UnfinishedInfo.NotifyType.Warning);

        IBrowser browser;
        try
        {
            browser = await GetBrowserAsync();
        }
        catch (PlaywrightException ex)
        {
            // El caso típico en planta: el instalador se armó sin la carpeta del navegador. Es un
            // problema de despliegue, no un fallo del programa, así que el usuario merece leerlo.
            return Returning.Unfinished(
                "No se encontró el navegador incrustado para generar el PDF. Reinstala la aplicación " +
                "o avisa a soporte.",
                ex.Message.Split('\n')[0],
                UnfinishedInfo.NotifyType.Warning);
        }

        var page = await browser.NewPageAsync();
        try
        {
            await page.SetContentAsync(BuildHtml(document), new PageSetContentOptions
            {
                WaitUntil = WaitUntilState.Load
            });

            return await page.PdfAsync(new PagePdfOptions
            {
                Format = "A4",
                PrintBackground = true,   // sin esto los avisos salen sin su color de fondo
                DisplayHeaderFooter = true,
                HeaderTemplate = "<div></div>",
                FooterTemplate =
                    "<div style=\"font-size:9px;width:100%;text-align:center;color:#666;\">" +
                    "Página <span class=\"pageNumber\"></span> de <span class=\"totalPages\"></span></div>",
                Margin = new Margin { Top = "18mm", Bottom = "18mm", Left = "16mm", Right = "16mm" }
            });
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    private async Task<IBrowser> GetBrowserAsync()
    {
        if (_browser is { IsConnected: true }) return _browser;

        await _gate.WaitAsync();
        try
        {
            if (_browser is { IsConnected: true }) return _browser;
            ObjectDisposedException.ThrowIf(_disposed, this);

            // Playwright busca los navegadores en la caché del USUARIO
            // (%LOCALAPPDATA%\ms-playwright) salvo que se le diga otra cosa, y ahí no hay nada en
            // una máquina de planta. Con "0" los busca junto al driver, es decir dentro de esta
            // misma carpeta de instalación. Lo fija la APLICACIÓN en su propio proceso: no hay que
            // configurar nada en el equipo del cliente.
            Environment.SetEnvironmentVariable("PLAYWRIGHT_BROWSERS_PATH", "0");

            _playwright ??= await Playwright.CreateAsync();
            _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Channel = HeadlessShellChannel
            });
            return _browser;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// Arma el documento completo en un solo HTML.
    ///
    /// Chromium NO tiene cargado el <c>knowledgehub.css</c> de la librería, así que el contenido
    /// llegaría sin estilo: los avisos sin su color, las tablas sin bordes. Por eso el ejemplo
    /// incrusta su propia hoja — y de paso enseña que con este motor el aspecto lo decides tú.
    /// </summary>
    private static string BuildHtml(PdfExportDocument document)
    {
        var html = new StringBuilder(1024 + document.Sections.Sum(s => s.ContentHtml.Length));
        html.Append("<!doctype html><html lang=\"es\"><head><meta charset=\"utf-8\"><style>")
            .Append(Css)
            .Append("</style></head><body>");

        // Portada.
        html.Append("<section class=\"cover\"><h1>").Append(WebUtility.HtmlEncode(document.Title)).Append("</h1>")
            .Append("<p>").Append(document.GeneratedAt.ToString("dd/MM/yyyy HH:mm")).Append("</p>");
        if (document.GeneratedBy is { } author)
            html.Append("<p>").Append(WebUtility.HtmlEncode(author)).Append("</p>");
        html.Append("</section>");

        var first = true;
        foreach (var section in document.Sections)
        {
            html.Append(first ? "<section>" : "<section class=\"page-break\">");
            first = false;

            var level = Math.Clamp(section.Level, 1, 6);
            html.Append("<h").Append(level).Append('>')
                .Append(WebUtility.HtmlEncode(section.Title))
                .Append("</h").Append(level).Append('>');

            html.Append(InlineImages(section.ContentHtml, document.Images));
            html.Append("</section>");
        }

        return html.Append("</body></html>").ToString();
    }

    /// <summary>
    /// Sustituye cada <c>docimg://{pk}</c> por un data URI. A diferencia de PDFsharp, aquí el WebP
    /// se incrusta TAL CUAL: Chromium lo lee de forma nativa y no hay que transcodificar nada.
    /// </summary>
    private static string InlineImages(string contentHtml, IReadOnlyDictionary<Guid, PdfExportImage> images) =>
        KnowledgeHubHtml.DocImgRegex().Replace(contentHtml, match =>
            images.TryGetValue(Guid.Parse(match.Groups["pk"].Value), out var image)
                ? $"data:{image.ContentType};base64,{Convert.ToBase64String(image.Content)}"
                : match.Value);   // la deja como está: se verá el enlace roto, no se pierde en silencio

    private const string Css = """
        body { font-family: 'Segoe UI', Arial, sans-serif; font-size: 11pt; color: #111; margin: 0; }
        h1 { font-size: 20pt; } h2 { font-size: 16pt; } h3 { font-size: 13pt; }
        .cover { text-align: center; padding-top: 8cm; page-break-after: always; }
        .cover h1 { font-size: 28pt; } .cover p { color: #666; }
        .page-break { page-break-before: always; }
        h1, h2, h3, h4, h5, h6 { page-break-after: avoid; }
        img { max-width: 100%; height: auto; }
        table { border-collapse: collapse; width: 100%; }
        th, td { border: 1px solid #bbb; padding: 4px 8px; text-align: left; }
        th { background: #f5f5f5; }
        pre { background: #f5f5f5; padding: 8px; border-radius: 4px; white-space: pre-wrap; }
        blockquote { border-left: 3px solid #ccc; margin-left: 0; padding-left: 12px; color: #555; }
        .kh-callout { border-radius: 6px; padding: 12px 16px; margin: 8px 0; }
        """;

    public async ValueTask DisposeAsync()
    {
        _disposed = true;
        if (_browser is not null) await _browser.DisposeAsync();
        _playwright?.Dispose();
        _gate.Dispose();
    }
}
