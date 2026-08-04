using Ganss.Xss;
using MgSoftDev.KnowledgeHub.Contracts;

namespace MgSoftDev.KnowledgeHub.HtmlSanitizer;

/// <summary>
/// Builds the <see cref="Ganss.Xss.HtmlSanitizer"/> instances KnowledgeHub uses. Start from
/// <see cref="CreateSanitizer()"/> and widen it (allow more tags, attributes or CSS properties)
/// instead of building one from scratch — the three adjustments the base makes are NOT optional
/// for KnowledgeHub content.
/// </summary>
public static class KnowledgeHubSanitizerDefaults
{
    /// <summary>Class marking a KnowledgeHub callout, whose styling survives every level.</summary>
    public const string CalloutClass = "kh-callout";

    /// <summary>
    /// Cosmetic properties stripped at <see cref="HtmlCleanupLevel.Strict"/>. Every
    /// <c>background-*</c> longhand is listed on purpose: AngleSharp expands the <c>background</c>
    /// shorthand, so removing only the shorthand leaves
    /// <c>background-position: initial; background-size: initial; …</c> behind.
    /// </summary>
    private static readonly string[] CosmeticCssProperties =
    [
        "color", "font", "font-family", "letter-spacing", "word-spacing", "white-space",
        "caret-color", "line-height", "text-shadow", "text-decoration-color", "opacity", "filter",
        "background", "background-color", "background-image", "background-position",
        "background-position-x", "background-position-y", "background-size", "background-repeat",
        "background-repeat-x", "background-repeat-y", "background-attachment", "background-origin",
        "background-clip", "background-blend-mode"
    ];

    /// <summary>Presentational leftovers from old HTML, stripped at Strict.</summary>
    private static readonly string[] CosmeticAttributes = ["bgcolor", "color", "face", "size", "align"];

    /// <summary>Wrappers that only carry styling; unwrapped at Strict (their text is kept).</summary>
    private static readonly string[] CosmeticTags = ["font", "span"];

    /// <summary>The permissive configuration (<see cref="HtmlCleanupLevel.Standard"/>).</summary>
    public static Ganss.Xss.HtmlSanitizer CreateSanitizer() => CreateSanitizer(HtmlCleanupLevel.Standard);

    /// <summary>
    /// A sanitizer configured for the given level. All levels keep the three KnowledgeHub-specific
    /// additions (the <c>data</c> and <c>docimg</c> schemes and the <c>zoom</c> CSS property);
    /// without them, cleaning would silently destroy pasted and stored images.
    /// </summary>
    public static Ganss.Xss.HtmlSanitizer CreateSanitizer(HtmlCleanupLevel level)
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

        // Callouts are identified by a class, so their styling can be spared below. 'class' is not
        // allowed out of the box; AllowedClasses then filters it down to just ours, which also
        // removes Word's MsoNormal and friends.
        sanitizer.AllowedAttributes.Add("class");
        sanitizer.AllowedClasses.Add(CalloutClass);

        if (level == HtmlCleanupLevel.Standard) return sanitizer;

        // Unwrapping must KEEP the text of the removed element. With the default (false), taking
        // 'span' out of the allow-list turns "<h2>T <span>inner</span></h2>" into "<h2>T </h2>" —
        // silent data loss.
        sanitizer.KeepChildNodes = true;

        if (level == HtmlCleanupLevel.Strict)
        {
            foreach (var property in CosmeticCssProperties) sanitizer.AllowedCssProperties.Remove(property);
            foreach (var attribute in CosmeticAttributes) sanitizer.AllowedAttributes.Remove(attribute);
            foreach (var tag in CosmeticTags) sanitizer.AllowedTags.Remove(tag);

            // …but never at the cost of what KnowledgeHub itself produces: image sizes and callout
            // colours are inline styles too. RemovingStyle fires per CSS property and can veto.
            sanitizer.RemovingStyle += (_, e) =>
            {
                if (IsProtectedElement(e.Tag)) e.Cancel = true;
            };
        }
        else // PlainText
        {
            sanitizer.AllowedTags.Clear();
            sanitizer.AllowedTags.Add("p");
            sanitizer.AllowedTags.Add("br");
            sanitizer.AllowedTags.Add("img");

            // Images keep their size; everything else loses styling entirely.
            sanitizer.RemovingStyle += (_, e) =>
            {
                if (IsImage(e.Tag)) e.Cancel = true;
            };
        }

        return sanitizer;
    }

    private static bool IsImage(AngleSharp.Dom.IElement element) =>
        string.Equals(element.TagName, "IMG", StringComparison.OrdinalIgnoreCase);

    private static bool IsProtectedElement(AngleSharp.Dom.IElement element) =>
        IsImage(element) || element.ClassList.Contains(CalloutClass);
}
