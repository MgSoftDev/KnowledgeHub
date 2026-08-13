using MgSoftDev.KnowledgeHub.Contracts;

namespace MgSoftDev.KnowledgeHub.HtmlSanitizer;

/// <summary>
/// What the host adds on top of the KnowledgeHub defaults. Its reason to exist is that the plain
/// <c>Action&lt;HtmlSanitizer&gt;</c> hook only ever reached level 1: levels 2 and 3 are built inside
/// <see cref="DefaultKnowledgeHubHtmlSanitizer"/> from the defaults, so a host had no way to say
/// "these classes are mine" for the cleanup button.
///
/// Declare it ONCE and apply it in every container that cleans. In a WASM setup that means the
/// client (which cleans on paste) AND the API server (which cleans on save) — configuring only one
/// of the two looks fine in the editor and then quietly strips the markup when it is stored.
/// </summary>
public sealed class KnowledgeHubSanitizerOptions
{
    /// <summary>
    /// Classes of the host that survive levels 1 and 2, added to the callout marker rather than
    /// replacing it. CSS class names are case-sensitive, hence the ordinal comparer.
    /// </summary>
    public ISet<string> AllowedClasses { get; } = new HashSet<string>(StringComparer.Ordinal);

    /// <summary>
    /// Whole families at once: <c>"kh-mi-"</c> covers <c>kh-mi-card</c>, <c>kh-mi-icon--orders</c>
    /// and every sibling added later. Worth preferring over listing names when the family grows
    /// with the design — a class nobody remembered to register is indistinguishable from junk, and
    /// gets dropped on save.
    /// </summary>
    public ISet<string> AllowedClassPrefixes { get; } = new HashSet<string>(StringComparer.Ordinal);

    /// <summary>
    /// Free-form tweaks to level 1, exactly what the <c>Action&lt;HtmlSanitizer&gt;</c> overload of
    /// <c>AddKnowledgeHubHtmlSanitizer</c> does. Level 1 is what SAVING uses, so this is the one
    /// that decides what ends up in the database.
    /// </summary>
    public Action<Ganss.Xss.HtmlSanitizer>? ConfigureStandard { get; set; }

    /// <summary>
    /// Escape hatch for ANY level: invoked once per level, last, after everything else. Use it for
    /// what the two class collections deliberately do not cover — for instance keeping <c>class</c>
    /// at <see cref="HtmlCleanupLevel.PlainText"/>, where the attribute is dropped wholesale
    /// because "text only" is the point of that level.
    /// </summary>
    public Action<Ganss.Xss.HtmlSanitizer, HtmlCleanupLevel>? ConfigureLevel { get; set; }
}
