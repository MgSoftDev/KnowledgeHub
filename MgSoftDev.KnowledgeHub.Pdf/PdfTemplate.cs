using MgSoftDev.KnowledgeHub.Dtos;

namespace MgSoftDev.KnowledgeHub.Pdf;

/// <summary>
/// A piece of the document the host decides: the cover, the header or the footer. Three levels,
/// most specific first — <see cref="Factory"/>, then <see cref="FilePath"/>, then <see cref="Html"/>.
/// Leave all three empty and the package uses its own.
///
/// <see cref="FilePath"/> is the one that makes per-company theming realistic: it is read on EVERY
/// export, so dropping a different file next to the executable changes the look with no rebuild
/// and no redeploy.
/// </summary>
public sealed class PdfTemplate
{
    /// <summary>
    /// HTML with placeholders: <c>{{Title}}</c>, <c>{{GeneratedAt}}</c>, <c>{{GeneratedBy}}</c>,
    /// <c>{{Logo}}</c> (an <c>&lt;img&gt;</c> tag, empty when no logo is configured) and
    /// <c>{{LogoSrc}}</c> (just the data URI, for your own tag).
    /// </summary>
    public string? Html { get; set; }

    /// <summary>
    /// File holding that same HTML, read on every export. A missing file is NOT an error: the
    /// template falls back to <see cref="Html"/> or to the default, because a theme that vanished
    /// must never stop someone from getting their document.
    /// </summary>
    public string? FilePath { get; set; }

    /// <summary>Full control from code. Wins over the other two.</summary>
    public Func<PdfTemplateContext, string>? Factory { get; set; }

    /// <summary>True when the host configured nothing and the package default applies.</summary>
    public bool IsEmpty =>
        Factory is null && string.IsNullOrWhiteSpace(FilePath) && string.IsNullOrWhiteSpace(Html);
}

/// <summary>What a template can use. Also the argument of <see cref="PdfTemplate.Factory"/>.</summary>
public sealed class PdfTemplateContext
{
    public required string Title { get; init; }

    public required DateTime GeneratedAt { get; init; }

    /// <summary>Who asked for the export; null when the host has no display name.</summary>
    public string? GeneratedBy { get; init; }

    /// <summary>How many pages the export contains.</summary>
    public required int SectionCount { get; init; }

    /// <summary>
    /// The configured logo, already a <c>data:</c> URI, or null. It is resolved for you because a
    /// header CANNOT load images by URL — see <see cref="KnowledgeHubPdfOptions.Header"/>.
    /// </summary>
    public string? LogoDataUri { get; init; }

    /// <summary>The document being exported, for templates that need more than the fields above.</summary>
    public required PdfExportDocument Document { get; init; }
}
