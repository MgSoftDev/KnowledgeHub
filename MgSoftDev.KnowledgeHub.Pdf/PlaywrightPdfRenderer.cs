using MgSoftDev.KnowledgeHub.Contracts;
using MgSoftDev.KnowledgeHub.Dtos;
using MgSoftDev.ReturningCore;
using MgSoftDev.ReturningCore.Helper;
using Microsoft.Playwright;

namespace MgSoftDev.KnowledgeHub.Pdf;

/// <summary>
/// Prints the document with Chromium, through Playwright. The PDF comes out exactly as the reader
/// shows it — CSS themes included — and there is no HTML-to-document mapper to keep up with
/// whatever the editor produces.
///
/// <b>Nothing stays resident.</b> Exporting is an occasional action, so the browser is launched,
/// used and disposed on every call rather than kept warm: a desktop application that runs for days
/// should not hold 200-300 MB for a button nobody may press. The trade is one or two seconds of
/// start-up per export.
///
/// Safe as a singleton: a semaphore serialises exports so two users never spawn two browsers.
/// </summary>
public sealed class PlaywrightPdfRenderer : IKnowledgeHubPdfRenderer, IDisposable
{
    private readonly KnowledgeHubPdfOptions _options;
    private readonly PdfHtmlBuilder _htmlBuilder;
    private readonly SemaphoreSlim _gate = new(1, 1);

    /// <summary>
    /// The source that worked last time, so Auto does not retry the whole chain on every export.
    /// It is just an enum — nothing is kept alive, which is the point of this renderer.
    /// </summary>
    private PdfBrowserSource? _resolvedSource;

    private bool _installAttempted;

    public PlaywrightPdfRenderer(KnowledgeHubPdfOptions? options = null)
    {
        _options = options ?? new KnowledgeHubPdfOptions();
        _htmlBuilder = new PdfHtmlBuilder(_options);
    }

    public async Task<Returning<byte[]>> RenderAsync(PdfExportDocument document)
    {
        if (document.Sections.Count == 0)
            return Returning.Unfinished("No hay contenido que exportar", UnfinishedInfo.NotifyType.Warning);

        await _gate.WaitAsync();
        try
        {
            return await RenderCoreAsync(document);
        }
        catch (PlaywrightException ex)
        {
            // Falta el navegador, o Chromium murió a mitad. Es un problema de despliegue o de
            // entorno, no un fallo del programa: el usuario merece leerlo, no una excepción.
            return Returning.Unfinished(NoBrowserMessage(), ex.Message.Split('\n')[0],
                UnfinishedInfo.NotifyType.Warning);
        }
        catch (TimeoutException)
        {
            return Returning.Unfinished(
                $"La generación del PDF superó los {_options.TimeoutSeconds} segundos y se canceló.",
                UnfinishedInfo.NotifyType.Warning);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<Returning<byte[]>> RenderCoreAsync(PdfExportDocument document)
    {
        var timeout = (float)TimeSpan.FromSeconds(_options.TimeoutSeconds).TotalMilliseconds;

        // Todo dentro del using: al salir no queda ni el navegador ni el proceso del driver.
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await LaunchAsync(playwright, timeout);
        var page = await browser.NewPageAsync();

        var logo = ResolveLogoDataUri();
        var html = _htmlBuilder.Build(document, logo);

        await page.SetContentAsync(html, new PageSetContentOptions
        {
            WaitUntil = WaitUntilState.Load,
            Timeout = timeout
        });

        var context = new PdfTemplateContext
        {
            Title = document.Title,
            GeneratedAt = document.GeneratedAt,
            GeneratedBy = document.GeneratedBy,
            SectionCount = document.Sections.Count,
            LogoDataUri = logo,
            Document = document
        };

        var header = _htmlBuilder.Resolve(_options.Header, context, _ => "<div></div>");
        var footer = _htmlBuilder.Resolve(_options.Footer, context, DefaultFooter);

        return await page.PdfAsync(new PagePdfOptions
        {
            Format = _options.PageFormat,
            Landscape = _options.Landscape,
            PrintBackground = true,          // sin esto los avisos salen sin su color de fondo
            Outline = _options.EmbedOutline, // marcadores navegables desde los encabezados

            // Outline NO funciona por su cuenta: Chromium construye el esquema a partir de la
            // estructura etiquetada, así que sin Tagged devuelve el PDF SIN marcadores y sin dar
            // ningún error. Comprobado midiendo: outline solo → 41.934 bytes y cero marcadores;
            // outline + tagged → 49.727 bytes y los títulos dentro. Se activa por nosotros para que
            // pedir marcadores signifique tenerlos. Coste: ~19% más de tamaño.
            Tagged = _options.TaggedPdf || _options.EmbedOutline,
            DisplayHeaderFooter = true,
            HeaderTemplate = header,
            FooterTemplate = footer,
            Margin = new Margin
            {
                Top = _options.Margins.Top,
                Bottom = _options.Margins.Bottom,
                Left = _options.Margins.Left,
                Right = _options.Margins.Right
            }
        });
    }

    /// <summary>
    /// Opens the browser according to <see cref="KnowledgeHubPdfOptions.BrowserSource"/>. In Auto it
    /// walks the chain — installed browser, bundled copy, Playwright cache — and remembers which one
    /// answered.
    /// </summary>
    private async Task<IBrowser> LaunchAsync(IPlaywright playwright, float timeout)
    {
        var source = _resolvedSource ?? _options.BrowserSource;

        if (source != PdfBrowserSource.Auto)
        {
            var browser = await LaunchFromAsync(playwright, source, timeout);
            _resolvedSource = source;
            return browser;
        }

        PlaywrightException? last = null;
        foreach (var candidate in new[]
                 {
                     PdfBrowserSource.SystemBrowser,
                     PdfBrowserSource.BundledWithApp,
                     PdfBrowserSource.PlaywrightCache
                 })
        {
            try
            {
                var browser = await LaunchFromAsync(playwright, candidate, timeout);
                _resolvedSource = candidate;
                return browser;
            }
            catch (PlaywrightException ex)
            {
                last = ex;   // se prueba el siguiente; si fallan todos, se relanza el último
            }
        }

        throw last!;
    }

    private async Task<IBrowser> LaunchFromAsync(IPlaywright playwright, PdfBrowserSource source, float timeout)
    {
        // Playwright lee esta variable al arrancar el navegador. Se fija AQUÍ, en el proceso de la
        // aplicación: instalar con PLAYWRIGHT_BROWSERS_PATH=0 no basta, porque en ejecución seguiría
        // buscando en la caché del usuario. El equipo del cliente no configura nada.
        Environment.SetEnvironmentVariable("PLAYWRIGHT_BROWSERS_PATH", BrowsersPathFor(source));

        if (source == PdfBrowserSource.DownloadOnDemand) EnsureInstalled();

        return await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Channel = source == PdfBrowserSource.SystemBrowser
                ? _options.SystemBrowserChannel
                : _options.BundledBrowser,
            Timeout = timeout
        });
    }

    private string? BrowsersPathFor(PdfBrowserSource source)
    {
        if (_options.BrowsersPath is { Length: > 0 } explicitPath) return explicitPath;

        return source switch
        {
            // "0" = junto al driver, dentro de la carpeta de la aplicación.
            PdfBrowserSource.BundledWithApp => "0",
            // El navegador del sistema no vive en ninguna carpeta de Playwright.
            PdfBrowserSource.SystemBrowser => null,
            _ => null   // caché por usuario, el comportamiento de fábrica
        };
    }

    /// <summary>Installs the browser on first use. Only for <see cref="PdfBrowserSource.DownloadOnDemand"/>.</summary>
    private void EnsureInstalled()
    {
        if (_installAttempted) return;
        _installAttempted = true;

        // Una sola vez por proceso: si no hay internet fallará, y el mensaje de "sin navegador"
        // explica el resto. Reintentarlo en cada exportación solo alargaría la espera.
        Microsoft.Playwright.Program.Main(["install", _options.BundledBrowser]);
    }

    private string? ResolveLogoDataUri()
    {
        var bytes = _options.LogoBytes;

        if (bytes is null && _options.LogoFilePath is { Length: > 0 } path)
        {
            try
            {
                if (File.Exists(path)) bytes = File.ReadAllBytes(path);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        return bytes is { Length: > 0 }
            ? $"data:{_options.LogoContentType};base64,{Convert.ToBase64String(bytes)}"
            : null;
    }

    private static string DefaultFooter(PdfTemplateContext _) =>
        // Estilos EN LÍNEA a propósito: la cabecera y el pie se renderizan en un contexto aislado
        // al que no llega el CSS del documento.
        "<div style=\"font-size:9px;width:100%;text-align:center;color:#6b7280;\">" +
        "Página <span class=\"pageNumber\"></span> de <span class=\"totalPages\"></span></div>";

    private string NoBrowserMessage() => _options.BrowserSource switch
    {
        PdfBrowserSource.SystemBrowser =>
            $"No se pudo abrir el navegador del sistema ({_options.SystemBrowserChannel}). " +
            "Comprueba que Microsoft Edge está instalado.",
        PdfBrowserSource.BundledWithApp =>
            "No se encontró el navegador incrustado en la carpeta de la aplicación. " +
            "Reinstala la aplicación o avisa a soporte.",
        PdfBrowserSource.PlaywrightCache =>
            $"No se encontró {_options.BundledBrowser} en la caché de Playwright. Ejecuta " +
            "`playwright install` en este equipo, y comprueba que la versión instalada coincide " +
            "con la del paquete Microsoft.Playwright que usa la aplicación.",
        PdfBrowserSource.DownloadOnDemand =>
            "No se pudo descargar el navegador necesario para generar el PDF. " +
            "Comprueba la conexión a internet.",
        _ =>
            "No hay ningún navegador disponible para generar el PDF: ni Microsoft Edge instalado, " +
            "ni una copia incrustada en la aplicación, ni la caché de Playwright."
    };

    public void Dispose() => _gate.Dispose();
}
