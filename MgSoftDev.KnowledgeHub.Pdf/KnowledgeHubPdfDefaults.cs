using MigraDoc.DocumentObjectModel;
using PdfSharp.Fonts;

namespace MgSoftDev.KnowledgeHub.Pdf;

/// <summary>Shared setup for the generated documents.</summary>
public static class KnowledgeHubPdfDefaults
{
    /// <summary>Class KnowledgeHub marks its callouts with; kept in sync with the Blazor package.</summary>
    public const string CalloutClass = "kh-callout";

    private static readonly Lock FontGate = new();
    private static bool _fontsConfigured;

    /// <summary>
    /// Makes sure PDFsharp can resolve a font, once per process.
    ///
    /// <c>GlobalFontSettings</c> is static for the whole process, so a host that already set it up
    /// must not be trampled: the resolver slot is only ever written when it is still empty. Under
    /// Windows the built-in system-font switch is enough and no resolver is needed at all.
    ///
    /// With nothing resolvable, rendering throws with a message pointing at font resolution — it
    /// fails loudly rather than producing a document full of blank glyphs.
    /// </summary>
    public static void EnsureFonts()
    {
        if (_fontsConfigured) return;

        lock (FontGate)
        {
            if (_fontsConfigured) return;

            if (OperatingSystem.IsWindows())
                GlobalFontSettings.UseWindowsFontsUnderWindows = true;

            _fontsConfigured = true;
        }
    }

    /// <summary>Applies the base styles every generated document shares.</summary>
    public static void ApplyStyles(Document document, KnowledgeHubPdfOptions options)
    {
        var normal = document.Styles["Normal"]!;
        normal.Font.Name = options.FontFamily;
        normal.Font.Size = options.FontSize;
        normal.ParagraphFormat.SpaceAfter = "0.15cm";

        // OutlineLevel is what turns a heading into a PDF bookmark, so the reader gets a navigable
        // sidebar for free.
        for (var level = 1; level <= 6; level++)
        {
            var style = document.Styles[$"Heading{level}"]!;
            style.Font.Name = options.FontFamily;
            style.Font.Bold = true;
            style.Font.Size = Math.Max(options.FontSize + 8 - (level * 1.5), options.FontSize);
            style.ParagraphFormat.SpaceBefore = "0.45cm";
            style.ParagraphFormat.SpaceAfter = "0.2cm";
            style.ParagraphFormat.OutlineLevel = (OutlineLevel)level;
            style.ParagraphFormat.KeepWithNext = true;
        }

        var toc = document.Styles.AddStyle("KhToc", "Normal");
        toc.ParagraphFormat.AddTabStop("15.5cm", TabAlignment.Right, TabLeader.Dots);
        toc.ParagraphFormat.SpaceAfter = "0.1cm";
    }
}
