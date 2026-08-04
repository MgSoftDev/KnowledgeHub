using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;
using MgSoftDev.KnowledgeHub.Contracts;

namespace MgSoftDev.KnowledgeHub.HtmlSanitizer;

/// <summary>
/// Fixes two things the sanitizer alone cannot, because both need the ORIGINAL structure and
/// HtmlSanitizer's own post-process events run after the tags are already flattened:
///
/// 1. With <c>KeepChildNodes = true</c> the text inside &lt;script&gt;/&lt;style&gt; leaks into the
///    output as visible junk (<c>okalert(1)p{color:red}</c>). Those nodes are dropped whole.
/// 2. Flattening inserts no separator, so at PlainText level "&lt;h2&gt;Title&lt;/h2&gt;&lt;p&gt;One&lt;/p&gt;"
///    collapses into "TitleOne". Block elements are turned into paragraphs first, which the
///    sanitizer then keeps.
/// </summary>
internal static class HtmlPreProcessor
{
    private static readonly HtmlParser Parser = new();

    /// <summary>Nodes whose textual content must never reach the output.</summary>
    private static readonly string[] DropWholeTags = ["script", "style", "noscript", "template", "head"];

    /// <summary>Block elements rewritten as paragraphs so their text stays separated.</summary>
    private static readonly string[] BlockTags =
        ["h1", "h2", "h3", "h4", "h5", "h6", "div", "li", "tr", "blockquote", "pre", "figcaption", "dd", "dt"];

    /// <summary>Returns the html ready to be sanitized at that level. Standard needs no changes.</summary>
    public static string Prepare(string html, HtmlCleanupLevel level)
    {
        if (level == HtmlCleanupLevel.Standard || string.IsNullOrEmpty(html)) return html;

        var document = Parser.ParseDocument(html);

        foreach (var element in document.QuerySelectorAll(string.Join(',', DropWholeTags)).ToList())
            element.Remove();

        if (level == HtmlCleanupLevel.PlainText)
            foreach (var element in document.QuerySelectorAll(string.Join(',', BlockTags)).ToList())
                ReplaceWithParagraph(document, element);

        return document.Body?.InnerHtml ?? string.Empty;
    }

    /// <summary>
    /// Swaps a block element for a &lt;p&gt; carrying the same children. Done depth-first by
    /// QuerySelectorAll order, so nested blocks are handled before their ancestors are replaced.
    /// </summary>
    private static void ReplaceWithParagraph(IHtmlDocument document, IElement element)
    {
        // Already detached because an ancestor was replaced first.
        if (element.ParentElement is null) return;

        var paragraph = document.CreateElement("p");
        while (element.FirstChild is { } child)
        {
            element.RemoveChild(child);
            paragraph.AppendChild(child);
        }

        element.Replace(paragraph);
    }
}
