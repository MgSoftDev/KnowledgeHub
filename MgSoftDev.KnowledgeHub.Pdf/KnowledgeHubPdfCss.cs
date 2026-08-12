namespace MgSoftDev.KnowledgeHub.Pdf;

/// <summary>
/// The stylesheet the PDF uses when the host configures none. It mirrors the content rules of the
/// module's own <c>knowledgehub.css</c> (headings, tables, code, images, callouts) so the document
/// looks like the reader out of the box — Chromium starts from a blank page and does not have the
/// RCL's stylesheet loaded.
///
/// Everything here is meant to be overridden: append with <c>AdditionalCss</c>, replace with
/// <c>Css</c>, or drop a file next to the executable with <c>CssFilePath</c>.
/// </summary>
public static class KnowledgeHubPdfCss
{
    public const string Default = """
        :root {
            --kh-pdf-text: #111827;
            --kh-pdf-muted: #6b7280;
            --kh-pdf-rule: #d1d5db;
            --kh-pdf-accent: #2563eb;
            --kh-pdf-code-bg: #f5f5f5;
        }

        body {
            font-family: 'Segoe UI', system-ui, Arial, sans-serif;
            font-size: 11pt;
            line-height: 1.5;
            color: var(--kh-pdf-text);
            margin: 0;
        }

        h1, h2, h3, h4, h5, h6 { line-height: 1.25; page-break-after: avoid; }
        h1 { font-size: 20pt; margin: 0 0 .5em; }
        h2 { font-size: 16pt; margin: 1.2em 0 .4em; }
        h3 { font-size: 13pt; margin: 1em 0 .3em; }
        h4, h5, h6 { font-size: 11.5pt; margin: .9em 0 .3em; }

        p, ul, ol { margin: 0 0 .7em; }
        li { margin-bottom: .2em; }
        a { color: var(--kh-pdf-accent); }

        img { max-width: 100%; height: auto; }
        figure { margin: 1em 0; }
        figcaption { font-size: 9pt; color: var(--kh-pdf-muted); font-style: italic; }

        table { border-collapse: collapse; width: 100%; margin: .8em 0; page-break-inside: avoid; }
        th, td { border: 1px solid var(--kh-pdf-rule); padding: 5px 9px; text-align: left; vertical-align: top; }
        th { background: #f5f5f5; font-weight: 600; }

        pre {
            background: var(--kh-pdf-code-bg);
            padding: 10px 12px;
            border-radius: 4px;
            white-space: pre-wrap;
            word-break: break-word;
            font-size: 9.5pt;
            page-break-inside: avoid;
        }
        code { font-family: Consolas, 'Courier New', monospace; font-size: 9.5pt; }
        pre code { font-size: inherit; }

        blockquote {
            border-left: 3px solid var(--kh-pdf-rule);
            margin: .8em 0 .8em 0;
            padding-left: 14px;
            color: #4b5563;
        }

        /* Los avisos de KnowledgeHub traen su color en el style inline; esto solo pone la caja. */
        .kh-callout { border-radius: 6px; padding: 12px 16px; margin: .9em 0; page-break-inside: avoid; }
        .kh-callout p:last-child { margin-bottom: 0; }

        /* --- Estructura que añade el exportador ------------------------------------------- */
        .kh-pdf-cover { text-align: center; padding-top: 7cm; page-break-after: always; }
        .kh-pdf-cover h1 { font-size: 28pt; margin-bottom: .6em; }
        .kh-pdf-cover .kh-pdf-meta { color: var(--kh-pdf-muted); font-size: 11pt; }
        .kh-pdf-cover img { max-height: 3cm; margin-bottom: 1.5cm; }

        .kh-pdf-index { page-break-after: always; }
        .kh-pdf-index h2 { margin-top: 0; }
        .kh-pdf-index ol { list-style: none; padding-left: 0; }
        .kh-pdf-index li { margin: .3em 0; }
        .kh-pdf-index a { text-decoration: none; color: var(--kh-pdf-text); }

        .kh-pdf-section + .kh-pdf-section { page-break-before: always; }
        """;
}
