using System.Text.RegularExpressions;

namespace MgSoftDev.KnowledgeHub.HtmlSanitizer;

/// <summary>
/// Carries <c>{{ … }}</c> template expressions across the sanitizer untouched, by turning each one
/// into an HTML comment before cleaning and putting it back afterwards.
///
/// <para>
/// It exists because cleaning is a DOM round-trip and a template is not markup. Two things were
/// measured against this very sanitizer:
/// </para>
/// <list type="bullet">
/// <item>
/// A <c>{{ for }}</c> wrapping table rows is HOISTED OUT of the table. The HTML table parsing rules
/// foster-parent any text that is not inside a cell, so
/// <c>&lt;table&gt;{{ for e in equipos }}&lt;tr&gt;…{{ end }}&lt;/table&gt;</c> came back as
/// <c>{{ for e in equipos }}{{ end }}&lt;table&gt;…</c> — the loop survives and no longer wraps
/// anything. That is exactly the shape documentation needs (a table of equipment, a list of roles),
/// so without this the feature would be useless where it matters most.
/// </item>
/// <item>
/// <c>&lt;</c>, <c>&gt;</c> and <c>&amp;&amp;</c> inside an expression come back escaped, and Scriban has no
/// textual operators to fall back on — <c>lt</c> and <c>gt</c> do not exist in the language.
/// </item>
/// </list>
///
/// <para>
/// Comments are used as the vehicle because, unlike text, they are NOT foster-parented out of a
/// table: the parser inserts them where it finds them.
/// </para>
///
/// <para>
/// <b>Never apply this to a page that is not a template.</b> A protected region is handed back
/// unexamined, so <c>{{ &lt;script&gt;alert(1)&lt;/script&gt; }}</c> would ride straight through. That is
/// harmless when a template engine consumes the region and never emits it, and an injection when
/// the browser gets it as content.
/// </para>
/// </summary>
internal static partial class TemplateSyntaxShield
{
    private const string Marker = "khtpl:";

    /// <summary>
    /// Non-greedy so that two expressions on the same line stay separate, and single-line-mode so a
    /// block spanning several lines is still captured whole.
    /// </summary>
    [GeneratedRegex(@"\{\{.*?\}\}", RegexOptions.Singleline)]
    private static partial Regex ExpressionRegex();

    [GeneratedRegex(@"<!--khtpl:(?<i>\d+)-->")]
    private static partial Regex PlaceholderRegex();

    /// <summary>Replaces every expression with a comment, collecting the originals in order.</summary>
    public static string Protect(string html, List<string> expressions) =>
        ExpressionRegex().Replace(html, match =>
        {
            expressions.Add(match.Value);
            return $"<!--{Marker}{expressions.Count - 1}-->";
        });

    /// <summary>
    /// Puts the expressions back. An index that no longer matches anything is left as it is instead
    /// of throwing: a mangled placeholder must not cost the user the whole save.
    /// </summary>
    public static string Restore(string html, List<string> expressions) =>
        PlaceholderRegex().Replace(html, match =>
            int.TryParse(match.Groups["i"].Value, out var index) && index < expressions.Count
                ? expressions[index]
                : match.Value);

    /// <summary>True for the comments this class produces, so the sanitizer can be told to keep them.</summary>
    public static bool IsPlaceholder(string commentText) =>
        commentText.StartsWith(Marker, StringComparison.Ordinal);
}
