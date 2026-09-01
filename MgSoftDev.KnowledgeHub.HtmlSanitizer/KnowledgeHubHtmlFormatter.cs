using System.Text;
using AngleSharp;
using AngleSharp.Dom;
using AngleSharp.Html;
using AngleSharp.Html.Parser;
using MgSoftDev.KnowledgeHub.Contracts;

namespace MgSoftDev.KnowledgeHub.HtmlSanitizer;

/// <summary>
/// Indents html without changing a single thing the page renders.
///
/// <para>
/// AngleSharp's own <c>PrettyMarkupFormatter</c> is NOT used, and not for taste — it was read.
/// Its <c>Text()</c> starts with <c>data.Replace('\n', ' ')</c>, so it collapses a whole
/// <c>&lt;pre&gt;</c> of code onto one line (only <c>script</c> and <c>style</c> escape through the
/// literal-text path; <c>pre</c>, <c>code</c> and <c>textarea</c> go through <c>Text</c>). And its
/// <c>OpenTag()</c> breaks the line whenever the previous sibling is not a text node, with no notion
/// of block versus inline, so <c>&lt;b&gt;a&lt;/b&gt;&lt;i&gt;b&lt;/i&gt;</c> comes back rendering
/// "a b" instead of "ab". Its <c>preserveTextFormatting</c> constructor does not save it either: it
/// only covers DIRECT children, so the text of a <c>&lt;pre&gt;&lt;code&gt;</c> is a grandchild and
/// stays unprotected.
/// </para>
///
/// <para>
/// The rule here, and the reason it is safe: <b>whitespace is only ever inserted next to a block
/// boundary</b>. Collapsible whitespace at the edge of a line is not painted, and an anonymous
/// inline box holding nothing but whitespace between block boxes is not rendered at all. Inside a
/// run of inline elements — where a newline WOULD show up as a space — nothing is touched.
/// </para>
/// </summary>
internal sealed class KnowledgeHubHtmlFormatter : IKnowledgeHubHtmlFormatter
{
    private const string Indent = "  ";

    /// <summary>Collapsible whitespace per HTML. NOT char.IsWhiteSpace: that includes U+00A0,
    /// which is visible content and comes back out as <c>&amp;nbsp;</c>.</summary>
    private static readonly char[] Collapsible = [' ', '\t', '\n', '\r', '\f'];

    private static readonly HtmlParser Parser = new();

    private static readonly IMarkupFormatter Markup = HtmlMarkupFormatter.Instance;

    /// <summary>
    /// Elements that produce a block box, so breaking the line around them cannot be seen. Table
    /// parts are in: whitespace between rows and cells is ignored by the table layout.
    /// </summary>
    private static readonly HashSet<string> BlockTags = new(StringComparer.OrdinalIgnoreCase)
    {
        "address", "article", "aside", "blockquote", "caption", "col", "colgroup", "dd", "details",
        "dialog", "div", "dl", "dt", "fieldset", "figcaption", "figure", "footer", "form",
        "h1", "h2", "h3", "h4", "h5", "h6", "header", "hgroup", "hr", "legend", "li", "main",
        "menu", "nav", "ol", "p", "pre", "section", "summary", "table", "tbody", "td", "tfoot",
        "th", "thead", "tr", "ul"
    };

    /// <summary>
    /// Inside these, whitespace IS the content. Decided by walking up the ancestors, never by
    /// looking at direct children: in <c>&lt;pre&gt;&lt;code&gt;x&lt;/code&gt;&lt;/pre&gt;</c> the
    /// text hangs off the <c>code</c>. <c>code</c> is in the list even though it is not preformatted
    /// by default, because <c>code { white-space: pre }</c> is an everyday host rule and there is no
    /// way to see it from the DOM. It costs nothing: code never contains blocks.
    /// </summary>
    private static readonly HashSet<string> PreformattedTags = new(StringComparer.OrdinalIgnoreCase)
        { "pre", "code", "textarea", "script", "style" };

    /// <summary>Their content is markup-verbatim, so it must not be escaped on the way out.</summary>
    private static readonly HashSet<string> LiteralTextTags = new(StringComparer.OrdinalIgnoreCase)
        { "script", "style" };

    private static readonly HashSet<string> VoidTags = new(StringComparer.OrdinalIgnoreCase)
    {
        "area", "base", "br", "col", "embed", "hr", "img", "input", "link", "meta", "param",
        "source", "track", "wbr"
    };

    public string Format(string html)
    {
        if (string.IsNullOrWhiteSpace(html)) return html;

        // Same shield the sanitizer uses, and for the same measured reason (gotcha 33): parsing
        // hoists a {{ for }} that wraps table rows straight out of the table. Here it can be
        // unconditional — unlike in the sanitizer, this class decides nothing about what survives,
        // so protecting a region opens no door.
        var expressions = new List<string>();
        var shielded = TemplateSyntaxShield.Protect(html, expressions);

        var document = Parser.ParseDocument(shielded);
        var body = document.Body;
        if (body is null) return html;

        // Only the body is serialized, so anything the parser decided belongs in the head would be
        // dropped — and a formatter that silently loses content is worse than one that does nothing.
        // It takes a leading <script>, <style> or <meta> to land there, which the sanitizer removes
        // long before this anyway; when it happens, the document comes back untouched.
        if (document.Head?.ChildNodes.Length > 0) return html;

        Normalize(body);

        var output = new StringBuilder(shielded.Length + 256);
        WriteChildren(body, depth: 0, preformatted: false, output);

        return TemplateSyntaxShield.Restore(output.ToString(), expressions);
    }

    /// <summary>
    /// Undoes the indentation of a previous pass, which is what makes formatting idempotent — the
    /// alternative is whitespace piling up on every click.
    ///
    /// <para>
    /// It trims the whitespace RUN at a text node's edge, not the node: after one pass
    /// <c>&lt;div&gt;foo&lt;p&gt;x&lt;/p&gt;&lt;/div&gt;</c> holds a single node <c>"foo\n  "</c>,
    /// which is not whitespace-only, so anything that just deleted blank nodes would keep growing.
    /// And it trims only where a block boundary already swallows the space, so the meaningful gap in
    /// <c>&lt;b&gt;a&lt;/b&gt; &lt;i&gt;b&lt;/i&gt;</c> survives untouched.
    /// </para>
    /// </summary>
    private static void Normalize(IElement root)
    {
        foreach (var text in root.Descendants().OfType<IText>().ToList())
        {
            if (IsInsidePreformatted(text)) continue;

            var data = text.Data;
            if (IsBlockBoundary(text.PreviousSibling, text.ParentElement))
                data = data.TrimStart(Collapsible);
            if (IsBlockBoundary(text.NextSibling, text.ParentElement))
                data = data.TrimEnd(Collapsible);

            if (data.Length == 0) text.Remove();
            else text.Data = data;
        }
    }

    /// <summary>
    /// True when that side of a text node is a place where whitespace cannot be seen: the neighbour
    /// is a block element, or there is no neighbour and the container itself is a block.
    /// </summary>
    private static bool IsBlockBoundary(INode? sibling, IElement? parent) =>
        sibling is null ? parent is not null && IsBlock(parent) : sibling is IElement e && IsBlock(e);

    private static void WriteChildren(INode parent, int depth, bool preformatted, StringBuilder output)
    {
        foreach (var child in parent.ChildNodes)
        {
            switch (child)
            {
                case IElement element:
                    WriteElement(element, depth, preformatted, output);
                    break;

                case IText text:
                    output.Append(LiteralTextTags.Contains((parent as IElement)?.LocalName ?? string.Empty)
                        ? Markup.LiteralText(text)
                        : Markup.Text(text));
                    break;

                case IComment comment:
                    output.Append(Markup.Comment(comment));
                    break;
            }
        }
    }

    private static void WriteElement(IElement element, int depth, bool preformatted, StringBuilder output)
    {
        var block = !preformatted && IsBlock(element);

        // Rule 1: break before a block's opening tag. Whitespace right before a block box lands on a
        // line edge and is never painted.
        if (block && output.Length > 0) AppendBreak(output, depth);

        var isVoid = VoidTags.Contains(element.LocalName);
        output.Append(Markup.OpenTag(element, isVoid));
        if (isVoid) return;

        var inside = preformatted || PreformattedTags.Contains(element.LocalName);
        WriteChildren(element, depth + 1, inside, output);

        // Rule 2: break before the closing tag only when the element already holds blocks, so
        // <p>Hola <b>mundo</b></p> stays on one line. Safe for the same reason as rule 1 — trailing
        // whitespace inside a block is dropped — this half is only about looks.
        if (block && HasBlockChild(element)) AppendBreak(output, depth);

        output.Append(Markup.CloseTag(element, selfClosing: false));
    }

    private static void AppendBreak(StringBuilder output, int depth)
    {
        output.Append('\n');
        for (var i = 0; i < depth; i++) output.Append(Indent);
    }

    private static bool HasBlockChild(IElement element) => element.Children.Any(IsBlock);

    /// <summary>
    /// A block element carrying <c>display</c> in its inline style is treated as inline. It covers
    /// the common <c>&lt;li style="display:inline"&gt;</c>, where a newline WOULD show as a space.
    /// A host stylesheet that changes display by class is invisible from here — the one accepted
    /// limitation, documented.
    /// </summary>
    private static bool IsBlock(IElement element) =>
        BlockTags.Contains(element.LocalName) &&
        element.GetAttribute("style")?.Contains("display", StringComparison.OrdinalIgnoreCase) != true;

    private static bool IsInsidePreformatted(INode node)
    {
        for (var parent = node.ParentElement; parent is not null; parent = parent.ParentElement)
            if (PreformattedTags.Contains(parent.LocalName)) return true;
        return false;
    }
}
