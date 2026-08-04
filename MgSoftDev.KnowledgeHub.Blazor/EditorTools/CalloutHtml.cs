using System.Net;

namespace MgSoftDev.KnowledgeHub.Blazor.EditorTools;

/// <summary>Builds the inline-styled callout blocks the built-in tools insert. Public so hosts
/// can reuse the same visual language in their own custom tools.</summary>
public static class CalloutHtml
{
    /// <summary>
    /// Marks a callout so the stricter cleanup levels can spare its colours — everything else's
    /// inline styling is cosmetic noise, but this is content the user deliberately created.
    /// Callouts made before this class existed are indistinguishable from a pasted div.
    /// </summary>
    public const string CssClass = "kh-callout";

    /// <summary>
    /// The text lives in an inner paragraph (so pressing Enter inside adds a normal line
    /// instead of cloning the styled box) and a trailing empty paragraph is appended (so the
    /// caret lands outside the box and Enter continues the document normally).
    /// </summary>
    public static string Build(string bg, string border, string accent, string icon, string title, string text) =>
        $"<div class=\"{CssClass}\" style=\"background:{bg};border:1px solid {border};border-left:5px solid {accent};border-radius:8px;padding:16px;margin:8px 0;\">" +
        $"<p style=\"margin:0;\"><strong>{icon} {WebUtility.HtmlEncode(title)}:</strong> {WebUtility.HtmlEncode(text)}</p></div>" +
        "<p><br></p>";
}
