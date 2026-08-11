using AngleSharp.Dom;
using MgSoftDev.KnowledgeHub.Dtos;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Shapes;
using MigraDoc.DocumentObjectModel.Tables;

namespace MgSoftDev.KnowledgeHub.Pdf;

/// <summary>
/// Walks the stored HTML and writes it into a MigraDoc section.
///
/// Guiding rule, learned the hard way elsewhere in this library: <b>an element this class does not
/// know is never dropped — it recurses into it so its text survives</b>. Silently losing content is
/// far worse than rendering it plainly, because nobody notices until the document is already in
/// someone's hands.
/// </summary>
internal sealed class HtmlToMigraDoc
{
    private const string MonospaceFont = "Courier New";

    private static readonly HashSet<string> BlockTags = new(StringComparer.OrdinalIgnoreCase)
    {
        "p", "div", "h1", "h2", "h3", "h4", "h5", "h6", "ul", "ol", "li", "table", "thead", "tbody",
        "tr", "td", "th", "blockquote", "pre", "figure", "figcaption", "hr", "section", "article",
        "header", "footer", "dl", "dt", "dd"
    };

    private readonly Section _section;
    private readonly IReadOnlyDictionary<Guid, PdfExportImage> _images;
    private readonly KnowledgeHubPdfOptions _options;
    private readonly int _headingOffset;

    private Paragraph? _current;

    public HtmlToMigraDoc(Section section, IReadOnlyDictionary<Guid, PdfExportImage> images,
        KnowledgeHubPdfOptions options, int headingOffset)
    {
        _section = section;
        _images = images;
        _options = options;
        _headingOffset = headingOffset;
    }

    public void Write(INode root)
    {
        foreach (var child in root.ChildNodes.ToList()) WriteNode(child, default);
        _current = null;
    }

    // ---------------------------------------------------------------- nodes

    private void WriteNode(INode node, InlineStyle style)
    {
        if (node.NodeType == NodeType.Text)
        {
            AppendText(node.TextContent, style);
            return;
        }

        if (node is not IElement element) return;

        var tag = element.TagName.ToLowerInvariant();
        switch (tag)
        {
            case "script" or "style" or "noscript" or "template":
                return;   // their text is code, not content

            case "br":
                Paragraph().AddLineBreak();
                return;

            case "hr":
                EndParagraph();
                var rule = _section.AddParagraph();
                rule.Format.Borders.Top.Width = 0.5;
                rule.Format.SpaceBefore = "0.2cm";
                rule.Format.SpaceAfter = "0.2cm";
                EndParagraph();
                return;

            case "img":
                WriteImage(element);
                return;

            case "table":
                WriteTable(element);
                return;

            case "ul" or "ol":
                WriteList(element, ordered: tag == "ol");
                return;

            case "h1" or "h2" or "h3" or "h4" or "h5" or "h6":
                WriteHeading(element, tag[1] - '0');
                return;

            case "pre":
                WritePre(element);
                return;

            case "blockquote":
                WriteBlockquote(element);
                return;

            case "b" or "strong":
                WriteChildren(element, style with { Bold = true });
                return;
            case "i" or "em":
                WriteChildren(element, style with { Italic = true });
                return;
            case "u" or "ins":
                WriteChildren(element, style with { Underline = true });
                return;
            case "code" or "kbd" or "samp" or "tt":
                WriteChildren(element, style with { Monospace = true });
                return;
            case "sub":
                WriteChildren(element, style with { Subscript = true });
                return;
            case "sup":
                WriteChildren(element, style with { Superscript = true });
                return;

            case "a":
                var href = element.GetAttribute("href");
                WriteChildren(element, style with { Link = IsRenderableLink(href) ? href : null });
                return;

            case "div" when element.ClassList.Contains(KnowledgeHubPdfDefaults.CalloutClass):
                WriteCallout(element);
                return;
        }

        // Everything else: block tags break the paragraph, inline tags just carry on. Either way we
        // go INTO the element, so unknown markup costs formatting, never text.
        if (BlockTags.Contains(tag))
        {
            EndParagraph();
            WriteChildren(element, style);
            EndParagraph();
        }
        else
        {
            WriteChildren(element, style);
        }
    }

    private void WriteChildren(INode node, InlineStyle style)
    {
        foreach (var child in node.ChildNodes.ToList()) WriteNode(child, style);
    }

    // ---------------------------------------------------------------- blocks

    private void WriteHeading(IElement element, int level)
    {
        EndParagraph();
        // Offset by the page's depth so a branch export produces one coherent outline instead of
        // every page restarting at level 1.
        var styleName = $"Heading{Math.Clamp(level + _headingOffset, 1, 6)}";
        var paragraph = _section.AddParagraph();
        paragraph.Style = styleName;
        _current = paragraph;
        WriteChildren(element, default);
        EndParagraph();
    }

    private void WritePre(IElement element)
    {
        EndParagraph();
        var paragraph = _section.AddParagraph();
        paragraph.Format.Font.Name = MonospaceFont;
        paragraph.Format.Font.Size = _options.FontSize - 1;
        paragraph.Format.Shading.Color = Color.Parse("#F5F5F5");
        paragraph.Format.SpaceBefore = "0.2cm";
        paragraph.Format.SpaceAfter = "0.2cm";
        paragraph.Format.LeftIndent = "0.3cm";

        // Whitespace IS the content in a code block, so line breaks are preserved by hand.
        var lines = element.TextContent.Replace("\r\n", "\n").Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            if (i > 0) paragraph.AddLineBreak();
            paragraph.AddText(lines[i]);
        }
        EndParagraph();
    }

    private void WriteBlockquote(IElement element)
    {
        EndParagraph();
        var before = _section.Elements.Count;
        WriteChildren(element, default);
        EndParagraph();

        for (var i = before; i < _section.Elements.Count; i++)
            if (_section.Elements[i] is Paragraph p)
            {
                p.Format.LeftIndent = "0.6cm";
                p.Format.Borders.Left.Width = 2;
                p.Format.Borders.Left.Color = Color.Parse("#CCCCCC");
                p.Format.Font.Italic = true;
            }
    }

    private void WriteCallout(IElement element)
    {
        EndParagraph();
        var before = _section.Elements.Count;
        WriteChildren(element, default);
        EndParagraph();

        // The border colour is the callout's own accent, taken from its inline style so the four
        // built-in variants keep telling themselves apart in print.
        var accent = ReadBorderColour(element.GetAttribute("style")) ?? "#3B82F6";
        for (var i = before; i < _section.Elements.Count; i++)
            if (_section.Elements[i] is Paragraph p)
            {
                p.Format.LeftIndent = "0.4cm";
                p.Format.Borders.Left.Width = 3;
                p.Format.Borders.Left.Color = Color.Parse(accent);
            }
    }

    private void WriteList(IElement list, bool ordered)
    {
        EndParagraph();
        var first = true;
        foreach (var item in list.Children.Where(c => c.TagName.Equals("li", StringComparison.OrdinalIgnoreCase)))
        {
            var paragraph = _section.AddParagraph();
            paragraph.Format.ListInfo = new ListInfo
            {
                ListType = ordered ? ListType.NumberList1 : ListType.BulletList1,
                ContinuePreviousList = !first
            };
            paragraph.Format.LeftIndent = "0.5cm";
            _current = paragraph;
            WriteChildren(item, default);
            EndParagraph();
            first = false;
        }
    }

    private void WriteTable(IElement element)
    {
        EndParagraph();

        var rows = element.QuerySelectorAll("tr").ToList();
        if (rows.Count == 0) return;

        var columnCount = rows.Max(r => r.Children.Count(c =>
            c.TagName.Equals("td", StringComparison.OrdinalIgnoreCase) ||
            c.TagName.Equals("th", StringComparison.OrdinalIgnoreCase)));
        if (columnCount == 0) return;

        var table = _section.AddTable();
        table.Borders.Width = 0.5;
        table.Borders.Color = Color.Parse("#BBBBBB");

        var available = ContentWidthPoint();
        for (var i = 0; i < columnCount; i++) table.AddColumn(Unit.FromPoint(available / columnCount));

        foreach (var htmlRow in rows)
        {
            var row = table.AddRow();
            var cells = htmlRow.Children.Where(c =>
                c.TagName.Equals("td", StringComparison.OrdinalIgnoreCase) ||
                c.TagName.Equals("th", StringComparison.OrdinalIgnoreCase)).ToList();

            for (var i = 0; i < cells.Count && i < columnCount; i++)
            {
                var isHeader = cells[i].TagName.Equals("th", StringComparison.OrdinalIgnoreCase);
                var paragraph = row.Cells[i].AddParagraph();
                paragraph.Format.Font.Bold = isHeader;
                paragraph.Format.Font.Size = _options.FontSize - 1;
                paragraph.AddText(cells[i].TextContent.Trim());
            }
        }
        _current = null;
    }

    private void WriteImage(IElement element)
    {
        var src = element.GetAttribute("src");
        if (string.IsNullOrWhiteSpace(src)) return;

        if (!TryGetImagePk(src, out var pk) || !_images.TryGetValue(pk, out var image))
        {
            Placeholder("[imagen no encontrada]");
            return;
        }

        var name = PdfImageEncoder.ToMigraDocName(image, out var size);
        if (name is null)
        {
            Placeholder("[imagen no legible]");
            return;
        }

        EndParagraph();
        var holder = _section.AddParagraph();
        var picture = holder.AddImage(name);
        picture.LockAspectRatio = true;

        // Cap at the text width so a wide screenshot does not spill off the page; smaller images
        // keep their natural size instead of being blown up.
        var maxWidth = ContentWidthPoint();
        var naturalWidth = size.Width * 72.0 / 96.0;    // the editor authors in CSS pixels
        picture.Width = Unit.FromPoint(Math.Min(naturalWidth, maxWidth));

        var alt = element.GetAttribute("alt");
        if (!string.IsNullOrWhiteSpace(alt))
        {
            var caption = _section.AddParagraph(alt);
            caption.Format.Font.Size = _options.FontSize - 2;
            caption.Format.Font.Italic = true;
            caption.Format.Font.Color = Color.Parse("#666666");
        }
        EndParagraph();
    }

    // ---------------------------------------------------------------- inline

    private void AppendText(string? text, InlineStyle style)
    {
        if (string.IsNullOrEmpty(text)) return;

        // Collapse HTML whitespace, but keep a single separating space: trimming everything glues
        // words that were split across inline tags.
        var collapsed = CollapseWhitespace(text);
        if (collapsed.Length == 0) return;
        if (collapsed.Trim().Length == 0 && _current is null) return;

        var paragraph = Paragraph();

        if (style.Link is { } url)
        {
            var link = paragraph.AddHyperlink(url, HyperlinkType.Web);
            var linked = link.AddFormattedText(collapsed);
            linked.Underline = Underline.Single;
            linked.Color = Color.Parse("#1A56DB");
            Apply(linked, style);
            return;
        }

        Apply(paragraph.AddFormattedText(collapsed), style);
    }

    private void Apply(FormattedText text, InlineStyle style)
    {
        if (style.Bold) text.Bold = true;
        if (style.Italic) text.Italic = true;
        if (style.Underline) text.Underline = Underline.Single;
        if (style.Monospace)
        {
            text.Font.Name = MonospaceFont;
            text.Font.Size = _options.FontSize - 1;
        }
        if (style.Subscript) text.Subscript = true;
        if (style.Superscript) text.Superscript = true;
    }

    private void Placeholder(string message)
    {
        var text = Paragraph().AddFormattedText(message);
        text.Italic = true;
        text.Color = Color.Parse("#B91C1C");
    }

    private Paragraph Paragraph() => _current ??= _section.AddParagraph();

    private void EndParagraph() => _current = null;

    // ---------------------------------------------------------------- helpers

    /// <summary>
    /// Usable width between the margins, in points.
    ///
    /// Guarded on purpose: <c>PageSetup.PageWidth</c> reads back as 0 when only
    /// <c>PageFormat</c> was set, which turns this subtraction NEGATIVE and lays out tables off the
    /// page with their columns scrambled. The renderer sets the size explicitly, and this fallback
    /// makes sure a section built some other way still gets sane numbers.
    /// </summary>
    private double ContentWidthPoint()
    {
        var width = _section.PageSetup.PageWidth.Point
                    - _section.PageSetup.LeftMargin.Point
                    - _section.PageSetup.RightMargin.Point;

        return width > 1 ? width : Unit.FromCentimeter(16.6).Point;   // A4 menos márgenes de 2,2 cm
    }

    private static bool TryGetImagePk(string src, out Guid pk)
    {
        pk = Guid.Empty;
        if (!src.StartsWith(KnowledgeHubHtml.DocImgScheme, StringComparison.OrdinalIgnoreCase)) return false;
        return Guid.TryParse(src[KnowledgeHubHtml.DocImgScheme.Length..], out pk);
    }

    /// <summary>Only absolute web links survive: an in-document anchor means nothing in a PDF.</summary>
    private static bool IsRenderableLink(string? href) =>
        !string.IsNullOrWhiteSpace(href) &&
        (href.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
         href.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
         href.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase));

    private static string CollapseWhitespace(string text)
    {
        Span<char> buffer = text.Length <= 512 ? stackalloc char[text.Length] : new char[text.Length];
        var length = 0;
        var lastWasSpace = false;

        foreach (var c in text)
        {
            var isSpace = char.IsWhiteSpace(c) || c == ' ';
            if (isSpace)
            {
                if (lastWasSpace) continue;
                buffer[length++] = ' ';
                lastWasSpace = true;
            }
            else
            {
                buffer[length++] = c;
                lastWasSpace = false;
            }
        }
        return new string(buffer[..length]);
    }

    /// <summary>Pulls the accent colour out of a callout's inline <c>border-left</c>.</summary>
    private static string? ReadBorderColour(string? style)
    {
        if (string.IsNullOrWhiteSpace(style)) return null;

        var match = System.Text.RegularExpressions.Regex.Match(style,
            @"border-left\s*:[^;]*?(#[0-9a-fA-F]{6}|rgba?\(\s*(\d+)\s*,\s*(\d+)\s*,\s*(\d+))");
        if (!match.Success) return null;

        if (match.Groups[1].Value.StartsWith('#')) return match.Groups[1].Value;
        return $"#{int.Parse(match.Groups[2].Value):X2}{int.Parse(match.Groups[3].Value):X2}{int.Parse(match.Groups[4].Value):X2}";
    }

    private readonly record struct InlineStyle(
        bool Bold, bool Italic, bool Underline, bool Monospace,
        bool Subscript, bool Superscript, string? Link);
}
