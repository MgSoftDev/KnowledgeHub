namespace MgSoftDev.KnowledgeHub.Pdf;

/// <summary>How the PDF is produced and what it looks like.</summary>
public sealed class KnowledgeHubPdfOptions
{
    // ---------------------------------------------------------------- navegador

    /// <summary>Where Chromium comes from. See <see cref="PdfBrowserSource"/>.</summary>
    public PdfBrowserSource BrowserSource { get; set; } = PdfBrowserSource.Auto;

    /// <summary>
    /// Playwright channel used with <see cref="PdfBrowserSource.SystemBrowser"/>: <c>"msedge"</c>
    /// (default — present on every Windows 10/11), <c>"chrome"</c>, or their beta/dev variants.
    /// </summary>
    public string SystemBrowserChannel { get; set; } = "msedge";

    /// <summary>
    /// Which browser to install and launch when it is NOT the system one.
    /// <c>"chromium-headless-shell"</c> is the light build (265 MB against 412 MB) and is enough to
    /// print; use <c>"chromium"</c> if you need the full browser.
    /// </summary>
    public string BundledBrowser { get; set; } = "chromium-headless-shell";

    /// <summary>
    /// Explicit folder holding the browsers. Overrides everything else — for installers that put
    /// them somewhere of their own. Normally left null.
    /// </summary>
    public string? BrowsersPath { get; set; }

    /// <summary>
    /// Ceiling for a single export. A hung Chromium on a plant machine would otherwise leave the
    /// application waiting forever; better to fail with a message.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 60;

    // ---------------------------------------------------------------- papel

    /// <summary>Paper size: <c>A4</c>, <c>Letter</c>… anything Chromium accepts.</summary>
    public string PageFormat { get; set; } = "A4";

    public bool Landscape { get; set; }

    /// <summary>
    /// Page margins. Top and bottom must leave room for the header and footer — Chromium simply
    /// does NOT draw them when the margin is too small, without any error.
    /// </summary>
    public PdfMargins Margins { get; set; } = new();

    // ---------------------------------------------------------------- aspecto

    /// <summary>
    /// Replaces the package's stylesheet entirely. Use it when the theme is yours end to end;
    /// otherwise prefer <see cref="AdditionalCss"/>, which keeps the base rules.
    /// </summary>
    public string? Css { get; set; }

    /// <summary>Appended after the stylesheet in use, so it wins on equal specificity.</summary>
    public string? AdditionalCss { get; set; }

    /// <summary>
    /// A <c>.css</c> file read on EVERY export and appended last. This is what lets a company
    /// restyle its PDFs by dropping a file next to the executable, with no rebuild. A missing file
    /// is ignored on purpose: a theme that disappeared must not stop the export.
    /// </summary>
    public string? CssFilePath { get; set; }

    /// <summary>Logo bytes, offered to the templates as <c>{{Logo}}</c>.</summary>
    public byte[]? LogoBytes { get; set; }

    /// <summary>Media type of <see cref="LogoBytes"/> / the logo file.</summary>
    public string LogoContentType { get; set; } = "image/png";

    /// <summary>Logo file, read on every export. Takes second place to <see cref="LogoBytes"/>.</summary>
    public string? LogoFilePath { get; set; }

    // ---------------------------------------------------------------- contenido

    /// <summary>Cover page. Empty means the package's own. See <see cref="PdfTemplate"/>.</summary>
    public PdfTemplate Cover { get; set; } = new();

    /// <summary>
    /// Page header. <b>Chromium renders it in an ISOLATED context</b>: the document's CSS does not
    /// reach it, images only load as <c>data:</c> URIs (use <c>{{Logo}}</c>), styles must be
    /// inline, and the base font is tiny. Chromium's own classes are available:
    /// <c>pageNumber</c>, <c>totalPages</c>, <c>date</c>, <c>title</c>, <c>url</c>.
    /// Empty means no header.
    /// </summary>
    public PdfTemplate Header { get; set; } = new();

    /// <summary>Page footer, with the same restrictions as <see cref="Header"/>. Empty means "Página N de M".</summary>
    public PdfTemplate Footer { get; set; } = new();

    /// <summary>Include the cover page. Default true.</summary>
    public bool IncludeCover { get; set; } = true;

    /// <summary>
    /// Include an index of the exported pages, with links that jump inside the document. It carries
    /// no page numbers: Chromium does not know which page a section lands on until it has laid the
    /// document out, and there is no second pass. Skipped for single-page exports.
    /// </summary>
    public bool IncludeIndex { get; set; } = true;

    /// <summary>
    /// Embed the navigable outline (bookmarks) built from the headings. Default true.
    /// <para>
    /// Turning this on also turns on <see cref="TaggedPdf"/>, whether you asked for it or not:
    /// Chromium builds the outline out of the tagged structure, and on its own this option is
    /// silently ignored — you get a PDF with no bookmarks and no error at all. Cost of having them:
    /// roughly 19% more bytes.
    /// </para>
    /// </summary>
    public bool EmbedOutline { get; set; } = true;

    /// <summary>
    /// Produce a tagged (accessible) PDF. Implied by <see cref="EmbedOutline"/>; set it on its own
    /// when you want accessibility without bookmarks.
    /// </summary>
    public bool TaggedPdf { get; set; }

    /// <summary>
    /// What the exporter does with the page's own title at the top of each section.
    /// <para>
    /// It defaults to <see cref="PdfSectionTitleMode.Auto"/> because the previous behaviour printed
    /// TWO titles on any page whose author had written their own <c>&lt;h1&gt;</c> — a very common
    /// habit, since the page name is often shortened for the tree while the document spells it out.
    /// Auto can only ever remove a duplicate, never leave a page untitled. Set
    /// <see cref="PdfSectionTitleMode.TreeLevel"/> to get exactly what earlier versions produced.
    /// </para>
    /// <para>
    /// Whatever the mode, the index keeps working: the anchor it links to lives on the section, not
    /// on the heading. What DOES change when a title is dropped is the PDF outline — Chromium builds
    /// it from the headings, so that page's entry becomes the author's own heading instead of an
    /// entry nested by its depth in the tree.
    /// </para>
    /// </summary>
    public PdfSectionTitleMode SectionTitle { get; set; } = PdfSectionTitleMode.Auto;
}

/// <summary>How each exported page is titled in the PDF.</summary>
public enum PdfSectionTitleMode
{
    /// <summary>Add the page's title only when the content does not already open with an
    /// <c>&lt;h1&gt;</c> of its own. The default.</summary>
    Auto,

    /// <summary>Always add it, sized by depth in the exported branch: h1 for the root, h2 for its
    /// children, and so on. What versions before 0.22 did.</summary>
    TreeLevel,

    /// <summary>Always add it, always as <c>&lt;h1&gt;</c>, so it does not shrink with depth.</summary>
    Heading1,

    /// <summary>Never add it — only what the author wrote shows up.</summary>
    Hidden
}

/// <summary>Page margins, in any CSS length Chromium accepts.</summary>
public sealed class PdfMargins
{
    public string Top { get; set; } = "18mm";
    public string Bottom { get; set; } = "18mm";
    public string Left { get; set; } = "16mm";
    public string Right { get; set; } = "16mm";
}
