using System.Net;
using System.Text;
using MgSoftDev.KnowledgeHub.Dtos;

namespace MgSoftDev.KnowledgeHub.Pdf;

/// <summary>
/// Turns the export document into the single HTML page Chromium prints. No DOM parsing: the
/// content HTML is concatenated as-is, which is the whole point of using a browser — whatever the
/// editor produces today or tomorrow renders exactly as it does on screen.
/// </summary>
internal sealed class PdfHtmlBuilder
{
    private readonly KnowledgeHubPdfOptions _options;

    public PdfHtmlBuilder(KnowledgeHubPdfOptions options) => _options = options;

    public string Build(PdfExportDocument document, string? logoDataUri)
    {
        var context = new PdfTemplateContext
        {
            Title = document.Title,
            GeneratedAt = document.GeneratedAt,
            GeneratedBy = document.GeneratedBy,
            SectionCount = document.Sections.Count,
            LogoDataUri = logoDataUri,
            Document = document
        };

        var anchors = document.Sections
            .Select((s, i) => (s.PagePk, Anchor: $"kh-sec-{i}"))
            .ToDictionary(x => x.PagePk, x => x.Anchor);

        var html = new StringBuilder(2048 + document.Sections.Sum(s => s.ContentHtml.Length));
        html.Append("<!doctype html><html lang=\"es\"><head><meta charset=\"utf-8\"><title>")
            .Append(WebUtility.HtmlEncode(document.Title))
            .Append("</title><style>")
            .Append(BuildCss())
            .Append("</style></head><body>");

        if (_options.IncludeCover) html.Append(RenderCover(context));
        if (_options.IncludeIndex && document.Sections.Count > 1) html.Append(RenderIndex(document, anchors));

        foreach (var section in document.Sections)
        {
            var level = Math.Clamp(section.Level, 1, 6);
            html.Append("<section class=\"kh-pdf-section\">")
                .Append("<h").Append(level).Append(" id=\"").Append(anchors[section.PagePk]).Append("\">")
                .Append(WebUtility.HtmlEncode(section.Title))
                .Append("</h").Append(level).Append('>')
                .Append(InlineImages(section.ContentHtml, document.Images))
                .Append("</section>");
        }

        return html.Append("</body></html>").ToString();
    }

    /// <summary>Base stylesheet (or the host's), then the extras, then the file — el último manda.</summary>
    private string BuildCss()
    {
        var css = new StringBuilder(_options.Css ?? KnowledgeHubPdfCss.Default);

        if (!string.IsNullOrWhiteSpace(_options.AdditionalCss))
            css.Append('\n').Append(_options.AdditionalCss);

        // Un tema que desapareció no puede impedir que alguien se lleve su documento.
        if (ReadFileOrNull(_options.CssFilePath) is { } fromFile)
            css.Append('\n').Append(fromFile);

        return css.ToString();
    }

    private string RenderCover(PdfTemplateContext context) =>
        $"<section class=\"kh-pdf-cover\">{Resolve(_options.Cover, context, DefaultCover)}</section>";

    private static string DefaultCover(PdfTemplateContext c)
    {
        var html = new StringBuilder();
        if (c.LogoDataUri is not null) html.Append("<img src=\"").Append(c.LogoDataUri).Append("\" alt=\"\">");
        html.Append("<h1>").Append(WebUtility.HtmlEncode(c.Title)).Append("</h1>")
            .Append("<div class=\"kh-pdf-meta\">").Append(c.GeneratedAt.ToString("dd/MM/yyyy HH:mm"));
        if (c.GeneratedBy is { } author)
            html.Append("<br>").Append(WebUtility.HtmlEncode(author));
        return html.Append("</div>").ToString();
    }

    private static string RenderIndex(PdfExportDocument document, IReadOnlyDictionary<Guid, string> anchors)
    {
        var html = new StringBuilder("<section class=\"kh-pdf-index\"><h2>Contenido</h2><ol>");
        foreach (var section in document.Sections)
        {
            // Sin números de página: Chromium no sabe dónde cae cada sección hasta maquetar. El
            // enlace interno sí funciona, y los marcadores del PDF cubren la navegación.
            var indent = Math.Max(section.Level - 1, 0) * 16;
            html.Append("<li style=\"margin-left:").Append(indent).Append("px\"><a href=\"#")
                .Append(anchors[section.PagePk]).Append("\">")
                .Append(WebUtility.HtmlEncode(section.Title))
                .Append("</a></li>");
        }
        return html.Append("</ol></section>").ToString();
    }

    /// <summary>
    /// Chromium reads WebP natively, so the stored bytes go in untouched — no transcoding step and
    /// nothing that can silently drop an image.
    /// </summary>
    private static string InlineImages(string contentHtml, IReadOnlyDictionary<Guid, PdfExportImage> images) =>
        KnowledgeHubHtml.DocImgRegex().Replace(contentHtml, match =>
            images.TryGetValue(Guid.Parse(match.Groups["pk"].Value), out var image)
                ? $"data:{image.ContentType};base64,{Convert.ToBase64String(image.Content)}"
                : match.Value);   // se deja tal cual: enlace roto visible, nunca pérdida silenciosa

    /// <summary>Factory → file → inline html → package default.</summary>
    public string Resolve(PdfTemplate template, PdfTemplateContext context,
        Func<PdfTemplateContext, string> fallback)
    {
        if (template.Factory is { } factory) return factory(context);

        var html = ReadFileOrNull(template.FilePath) ?? template.Html;
        return html is null ? fallback(context) : ApplyPlaceholders(html, context);
    }

    public static string ApplyPlaceholders(string template, PdfTemplateContext context)
    {
        var logoTag = context.LogoDataUri is null ? "" : $"<img src=\"{context.LogoDataUri}\" alt=\"\">";
        return template
            .Replace("{{Title}}", WebUtility.HtmlEncode(context.Title))
            .Replace("{{GeneratedAt}}", context.GeneratedAt.ToString("dd/MM/yyyy HH:mm"))
            .Replace("{{GeneratedBy}}", WebUtility.HtmlEncode(context.GeneratedBy ?? string.Empty))
            .Replace("{{SectionCount}}", context.SectionCount.ToString())
            .Replace("{{LogoSrc}}", context.LogoDataUri ?? string.Empty)
            .Replace("{{Logo}}", logoTag);
    }

    private static string? ReadFileOrNull(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        try
        {
            return File.Exists(path) ? File.ReadAllText(path) : null;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }
}
