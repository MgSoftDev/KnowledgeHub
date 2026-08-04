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

    private static readonly string BlockSelector = string.Join(',', BlockTags);

    /// <summary>
    /// Used to ask "does this element already contain a paragraph?". It adds <c>p</c> to the list
    /// above, because a &lt;p&gt; that is already a paragraph nests just as badly as one this class
    /// is about to create.
    /// </summary>
    private static readonly string ContainsBlockSelector = BlockSelector + ",p";

    /// <summary>Returns the html ready to be sanitized at that level. Standard needs no changes.</summary>
    public static string Prepare(string html, HtmlCleanupLevel level)
    {
        if (level == HtmlCleanupLevel.Standard || string.IsNullOrEmpty(html)) return html;

        var document = Parser.ParseDocument(html);

        foreach (var element in document.QuerySelectorAll(string.Join(',', DropWholeTags)).ToList())
            element.Remove();

        if (level == HtmlCleanupLevel.PlainText)
            foreach (var element in document.QuerySelectorAll(BlockSelector).ToList())
                Flatten(document, element);

        return document.Body?.InnerHtml ?? string.Empty;
    }

    /// <summary>
    /// Turns a block element into a paragraph, or unwraps it when it already CONTAINS blocks.
    /// Renaming a container would nest paragraphs (<c>&lt;p&gt;&lt;p&gt;Note&lt;/p&gt;&lt;/p&gt;</c>),
    /// which the parser then splits into two empty paragraphs around the real one — visible as
    /// stray blank lines. QuerySelectorAll returns document order, so a container is handled
    /// before the blocks it holds.
    /// </summary>
    private static void Flatten(IHtmlDocument document, IElement element)
    {
        // Already detached because an ancestor was unwrapped first.
        if (element.ParentElement is null) return;

        INode replacement = element.QuerySelector(ContainsBlockSelector) is null
            ? document.CreateElement("p")
            : document.CreateDocumentFragment();

        while (element.FirstChild is { } child)
        {
            element.RemoveChild(child);
            replacement.AppendChild(child);
        }

        element.Replace(replacement);
    }
}
