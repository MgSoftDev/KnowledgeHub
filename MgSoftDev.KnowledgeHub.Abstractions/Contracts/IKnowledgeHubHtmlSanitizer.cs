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
}

/// <summary>Origin of a <see cref="IKnowledgeHubHtmlSanitizer.Sanitize"/> call.</summary>
public enum HtmlSanitizeContext
{
    /// <summary>Content pasted into the editor (typically the dirtiest: Word, web pages…).</summary>
    Paste,

    /// <summary>Last checkpoint before writing the version to the database.</summary>
    Save,

    /// <summary>The user pressed the editor's cleanup button.</summary>
    Manual
}
