namespace MgSoftDev.KnowledgeHub.Contracts;

/// <summary>
/// Cleans untrusted HTML before it reaches the editor or the database. KnowledgeHub calls it when
/// content is pasted, when the user asks for a manual cleanup, and right before persisting a
/// version — so markup typed by hand in the Source view is checked too.
///
/// The implementation is OPTIONAL: when no sanitizer is registered the library behaves exactly as
/// before (no cleaning). The official default lives in the separate
/// <c>MgSoftDev.KnowledgeHub.HtmlSanitizer</c> package; a host may register its own instead to
/// widen or tighten the rules.
///
/// Implementations MUST preserve the two image reference formats KnowledgeHub relies on (see
/// <see cref="KnowledgeHubHtml"/>): the stored <c>docimg://{pk}</c> scheme and pasted
/// <c>data:image/...;base64,...</c> URIs — stripping either one silently destroys images.
/// </summary>
public interface IKnowledgeHubHtmlSanitizer
{
    /// <summary>
    /// Returns the cleaned HTML. Must be pure: same input, same output (the save path compares the
    /// result against the original to decide whether anything was removed).
    /// </summary>
    /// <param name="html">The HTML to clean; never null.</param>
    /// <param name="context">Where the call comes from, so hosts can vary strictness.</param>
    string Sanitize(string html, HtmlSanitizeContext context);

    /// <summary>
    /// Same, but at an explicit cleanup level chosen by the user in the editor toolbar. Only the
    /// paste path and the manual cleanup button pass a level; saving always uses the plain
    /// overload, so a level left selected can never strip a whole stored document.
    ///
    /// Default implementation ignores the level and falls back to the two-argument overload, so
    /// hosts that implemented this interface before levels existed keep compiling and behaving
    /// exactly as they did.
    /// </summary>
    string Sanitize(string html, HtmlSanitizeContext context, HtmlCleanupLevel level) =>
        Sanitize(html, context);
}

/// <summary>How aggressively to clean. Chosen per editor from the toolbar.</summary>
public enum HtmlCleanupLevel
{
    /// <summary>
    /// Permissive: drops Word junk, scripts and event handlers but keeps inline styles.
    /// The historical behaviour and the default.
    /// </summary>
    Standard,

    /// <summary>
    /// Keeps the HTML structure but strips cosmetic noise — background/text colours, fonts,
    /// letter/word spacing, white-space… — so pasted content adopts the site's look. Image sizes
    /// and KnowledgeHub callouts survive.
    /// </summary>
    Strict,

    /// <summary>
    /// Text only: nothing survives but paragraphs, line breaks and images.
    /// </summary>
    PlainText
}

/// <summary>Origin of a sanitize call, so hosts can vary strictness by where it came from.</summary>
public enum HtmlSanitizeContext
{
    /// <summary>Content pasted into the editor (typically the dirtiest: Word, web pages…).</summary>
    Paste,

    /// <summary>Last checkpoint before writing the version to the database.</summary>
    Save,

    /// <summary>The user pressed the editor's cleanup button.</summary>
    Manual
}
