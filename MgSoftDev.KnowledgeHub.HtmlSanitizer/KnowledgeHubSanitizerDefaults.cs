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
    /// The ONLY CSS kept at <see cref="HtmlCleanupLevel.Strict"/> — layout, not looks. It is an
    /// allow-list on purpose: the first attempt listed the cosmetic properties to remove instead,
    /// and real pastes sailed straight through it with whatever the site happened to use
    /// (<c>orphans</c>, <c>-webkit-*</c>…). An allow-list fails closed. It also sidesteps the
    /// <c>background</c> shorthand trap: AngleSharp expands it into longhands, so a deny-list has
    /// to name every single <c>background-*</c> or it leaves
    /// <c>background-position: initial; background-size: initial; …</c> behind.
    /// </summary>
    private static readonly string[] StructuralCssProperties =
    [
        "text-align", "vertical-align", "direction",
        "margin", "margin-top", "margin-right", "margin-bottom", "margin-left",
        "padding", "padding-top", "padding-right", "padding-bottom", "padding-left",
        "border", "border-top", "border-right", "border-bottom", "border-left",
        "border-width", "border-style", "border-radius", "border-collapse", "border-spacing",
        "width", "height", "max-width", "max-height", "min-width", "min-height", "zoom",
        "display", "float", "clear", "table-layout",
        "list-style", "list-style-type", "list-style-position"
    ];

    /// <summary>Presentational leftovers from old HTML, stripped at Strict.</summary>
    private static readonly string[] CosmeticAttributes = ["bgcolor", "color", "face", "size", "align"];

    /// <summary>Wrappers that only carry styling; unwrapped at Strict (their text is kept).</summary>
    private static readonly string[] CosmeticTags = ["font", "span"];

    /// <summary>Everything that survives <see cref="HtmlCleanupLevel.PlainText"/>.</summary>
    private static readonly string[] PlainTextTags = ["p", "br", "img"];

    /// <summary>
    /// Attributes kept at PlainText. <c>style</c> is here so images can keep their size — the CSS
    /// allow-list is emptied instead, and <see cref="ImageSizeProperties"/> is vetoed back in.
    /// </summary>
    private static readonly string[] PlainTextAttributes = ["src", "alt", "style"];

    /// <summary>What the image size tool writes; the only CSS that survives PlainText.</summary>
    private static readonly string[] ImageSizeProperties =
        ["width", "height", "max-width", "max-height", "zoom"];

    /// <summary>The permissive configuration (<see cref="HtmlCleanupLevel.Standard"/>).</summary>
    public static Ganss.Xss.HtmlSanitizer CreateSanitizer() => CreateSanitizer(HtmlCleanupLevel.Standard);

    /// <inheritdoc cref="CreateSanitizer(HtmlCleanupLevel, KnowledgeHubSanitizerOptions?)"/>
    public static Ganss.Xss.HtmlSanitizer CreateSanitizer(HtmlCleanupLevel level) =>
        CreateSanitizer(level, null);

    /// <summary>
    /// A sanitizer configured for the given level. All levels keep the three KnowledgeHub-specific
    /// additions (the <c>data</c> and <c>docimg</c> schemes and the <c>zoom</c> CSS property);
    /// without them, cleaning would silently destroy pasted and stored images.
    /// </summary>
    /// <param name="level">How aggressive the cleaning is.</param>
    /// <param name="options">
    /// What the host adds on top. Null keeps the historical behaviour exactly.
    /// </param>
    public static Ganss.Xss.HtmlSanitizer CreateSanitizer(HtmlCleanupLevel level,
        KnowledgeHubSanitizerOptions? options)
    {
        var sanitizer = new Ganss.Xss.HtmlSanitizer();

        // 1) Pasted images arrive inline and are only uploaded when the page is saved. Without
        //    this the sanitizer would drop them BEFORE the upload had a chance to run.
        sanitizer.AllowedSchemes.Add("data");

        // 2) docimg:// is the canonical stored image reference. Without this, sanitizing on save
        //    would silently delete every image already in the document.
        sanitizer.AllowedSchemes.Add("docimg");

        // 3) Callouts are identified by a class, so their styling can be spared below. 'class' is
        //    not allowed out of the box; AllowedClasses then filters it down to just ours, which
        //    also removes Word's MsoNormal and friends.
        sanitizer.AllowedAttributes.Add("class");
        sanitizer.AllowedClasses.Add(CalloutClass);

        // 4) Comments are stripped by default, and the template shield rides in comments. Only its
        //    own markers are spared — every other comment (Word's conditionals included) still goes.
        sanitizer.RemovingComment += (_, e) =>
        {
            if (TemplateSyntaxShield.IsPlaceholder(e.Comment.TextContent)) e.Cancel = true;
        };

        // 5) The host's own classes, so a documented layout survives being cleaned. They are ADDED
        //    to the marker above, never replacing it: the set must stay non-empty or the library
        //    switches to "allow everything" and Word's MsoNormal comes back in.
        if (options is not null)
            foreach (var cssClass in options.AllowedClasses)
                sanitizer.AllowedClasses.Add(cssClass);

        // Prefixes have no equivalent in AllowedClasses, which matches literally. RemovingCssClass
        // fires once per class about to go and can veto — same shape as the RemovingStyle hooks
        // below. Only wired when there is something to match, to keep the common case free.
        if (options is { AllowedClassPrefixes.Count: > 0 })
        {
            var prefixes = options.AllowedClassPrefixes.ToArray();
            sanitizer.RemovingCssClass += (_, e) =>
            {
                foreach (var prefix in prefixes)
                    if (e.CssClass.StartsWith(prefix, StringComparison.Ordinal))
                    {
                        e.Cancel = true;
                        return;
                    }
            };
        }

        switch (level)
        {
            case HtmlCleanupLevel.Standard:
                // 'zoom' is non-standard and NOT in the stock allow-list, but it is what the image
                // size tool writes (width/height already are allowed).
                sanitizer.AllowedCssProperties.Add("zoom");
                break;

            case HtmlCleanupLevel.Strict:
                // Unwrapping must KEEP the text of the removed element. With the default (false),
                // taking 'span' out of the allow-list turns "<h2>T <span>inner</span></h2>" into
                // "<h2>T </h2>" — silent data loss.
                sanitizer.KeepChildNodes = true;
                Replace(sanitizer.AllowedCssProperties, StructuralCssProperties);
                foreach (var attribute in CosmeticAttributes) sanitizer.AllowedAttributes.Remove(attribute);
                foreach (var tag in CosmeticTags) sanitizer.AllowedTags.Remove(tag);

                // …but never at the cost of what KnowledgeHub itself produces: image sizes and
                // callout colours are inline styles too. RemovingStyle fires per CSS property and
                // can veto, so both keep everything they were given.
                sanitizer.RemovingStyle += (_, e) =>
                {
                    if (IsProtectedElement(e.Tag)) e.Cancel = true;
                };
                break;

            default: // PlainText
                sanitizer.KeepChildNodes = true;
                Replace(sanitizer.AllowedTags, PlainTextTags);

                // Emptying AllowedTags is NOT enough: 'style' would still be allowed with the stock
                // 239-property list, so a pasted <p> kept its colours and fonts untouched. Both
                // lists have to be cut down too.
                Replace(sanitizer.AllowedAttributes, PlainTextAttributes);
                sanitizer.AllowedCssProperties.Clear();

                // With nothing allowed, every declaration is removed — images veto back the few
                // that carry their size, and lose anything cosmetic they were pasted with.
                sanitizer.RemovingStyle += (_, e) =>
                {
                    if (IsImage(e.Tag) && ImageSizeProperties.Contains(e.Style.Name, StringComparer.OrdinalIgnoreCase))
                        e.Cancel = true;
                };
                break;
        }

        // Last word to the host, after every rule above is in place — otherwise Strict and
        // PlainText would undo it when they REPLACE the allow-lists.
        if (level == HtmlCleanupLevel.Standard) options?.ConfigureStandard?.Invoke(sanitizer);
        options?.ConfigureLevel?.Invoke(sanitizer, level);

        return sanitizer;
    }

    private static void Replace(ISet<string> allowList, string[] values)
    {
        allowList.Clear();
        foreach (var value in values) allowList.Add(value);
    }

    private static bool IsImage(AngleSharp.Dom.IElement element) =>
        string.Equals(element.TagName, "IMG", StringComparison.OrdinalIgnoreCase);

    private static bool IsProtectedElement(AngleSharp.Dom.IElement element) =>
        IsImage(element) || element.ClassList.Contains(CalloutClass);
}
