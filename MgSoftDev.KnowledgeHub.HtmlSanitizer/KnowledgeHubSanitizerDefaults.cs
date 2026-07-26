using Ganss.Xss;

namespace MgSoftDev.KnowledgeHub.HtmlSanitizer;

/// <summary>
/// Builds the <see cref="Ganss.Xss.HtmlSanitizer"/> KnowledgeHub uses by default. Start from
/// <see cref="CreateSanitizer"/> and widen it (allow more tags, attributes or CSS properties)
/// instead of building one from scratch — the three adjustments it makes are NOT optional for
/// KnowledgeHub content.
/// </summary>
public static class KnowledgeHubSanitizerDefaults
{
    /// <summary>
    /// A sanitizer with the stock allow-lists plus the three KnowledgeHub-specific additions.
    /// Verified against HtmlSanitizer 9.1.x: the stock configuration already strips everything
    /// Word pastes in (<c>o:p</c>, <c>MsoNormal</c> classes, <c>v:shape</c>/<c>v:imagedata</c>
    /// and <c>&lt;!--[if …]&gt;</c> conditional comments) as well as scripts and event handlers.
    /// </summary>
    public static Ganss.Xss.HtmlSanitizer CreateSanitizer()
    {
        var sanitizer = new Ganss.Xss.HtmlSanitizer();

        // 1) Pasted images arrive inline and are only uploaded when the page is saved. Without
        //    this the sanitizer would drop them BEFORE the upload had a chance to run.
        sanitizer.AllowedSchemes.Add("data");

        // 2) docimg:// is the canonical stored image reference. Without this, sanitizing on save
        //    would silently delete every image already in the document.
        sanitizer.AllowedSchemes.Add("docimg");

        // 3) 'zoom' is non-standard and NOT in the stock allow-list, but it is what the image
        //    size tool writes (width/height already are allowed).
        sanitizer.AllowedCssProperties.Add("zoom");

        return sanitizer;
    }
}
