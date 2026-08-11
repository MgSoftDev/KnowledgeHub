namespace MgSoftDev.KnowledgeHub.Pdf;

/// <summary>Look and feel of the generated PDF.</summary>
public sealed class KnowledgeHubPdfOptions
{
    /// <summary>Font family used throughout. Must be resolvable — see <see cref="ConfigureFonts"/>.</summary>
    public string FontFamily { get; set; } = "Arial";

    /// <summary>Body text size in points.</summary>
    public double FontSize { get; set; } = 10;

    /// <summary>Add a cover page with the title, the date and who generated it.</summary>
    public bool IncludeCover { get; set; } = true;

    /// <summary>
    /// Add a table of contents with real page numbers. Ignored for a single-page export, where it
    /// would be a page listing one entry.
    /// </summary>
    public bool IncludeTableOfContents { get; set; } = true;

    /// <summary>Print "Página N de M" at the bottom of every page.</summary>
    public bool IncludePageNumbers { get; set; } = true;

    /// <summary>
    /// Let the package set up font resolution on first use.
    /// <para>
    /// PDFsharp resolves fonts through <c>GlobalFontSettings</c>, which is static for the WHOLE
    /// process. Two consequences worth knowing:
    /// </para>
    /// <para>
    /// Leave it true (default) and the package enables Windows system fonts under Windows, and
    /// only ever assigns a font resolver when nothing has claimed the slot yet — so a host that
    /// already configured PDFsharp keeps its own setup untouched.
    /// </para>
    /// <para>
    /// Set it to false if you configure <c>GlobalFontSettings</c> yourself. Doing so on a machine
    /// with no resolvable fonts makes rendering throw — loudly, not silently, so you will know.
    /// </para>
    /// </summary>
    public bool ConfigureFonts { get; set; } = true;
}
