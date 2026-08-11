using AngleSharp.Html.Parser;
using MgSoftDev.KnowledgeHub.Contracts;
using MgSoftDev.KnowledgeHub.Dtos;
using MgSoftDev.ReturningCore;
using MgSoftDev.ReturningCore.Helper;
using MigraDoc.DocumentObjectModel;
using MigraDoc.Rendering;

namespace MgSoftDev.KnowledgeHub.Pdf;

/// <summary>
/// Default <see cref="IKnowledgeHubPdfRenderer"/>, on PDFsharp/MigraDoc. Fully managed: no browser,
/// no native binary, nothing to install alongside.
///
/// Safe as a singleton — each call builds its own document and shares no state.
/// </summary>
public sealed class MigraDocPdfRenderer : IKnowledgeHubPdfRenderer
{
    private static readonly HtmlParser Parser = new();

    private readonly KnowledgeHubPdfOptions _options;

    public MigraDocPdfRenderer(KnowledgeHubPdfOptions? options = null)
    {
        _options = options ?? new KnowledgeHubPdfOptions();
    }

    public Task<Returning<byte[]>> RenderAsync(PdfExportDocument export) =>
        Returning<byte[]>.TryTask(() =>
        {
            if (export.Sections.Count == 0)
                return Task.FromResult<Returning<byte[]>>(
                    Returning.Unfinished("No hay contenido que exportar", UnfinishedInfo.NotifyType.Warning));

            if (_options.ConfigureFonts) KnowledgeHubPdfDefaults.EnsureFonts();

            var document = new Document();
            document.Info.Title = export.Title;
            if (export.GeneratedBy is { } author) document.Info.Author = author;
            KnowledgeHubPdfDefaults.ApplyStyles(document, _options);

            // Bookmarks have to exist before the table of contents can point at them, but MigraDoc
            // resolves the page numbers at render time, so the order of writing does not matter.
            var bookmarks = export.Sections
                .Select((s, i) => (s.PagePk, Name: $"kh-{i}"))
                .ToDictionary(x => x.PagePk, x => x.Name);

            var multiPage = export.Sections.Count > 1;
            if (_options.IncludeCover) WriteCover(document, export);
            if (_options.IncludeTableOfContents && multiPage) WriteToc(document, export, bookmarks);

            WriteBody(document, export, bookmarks);

            var renderer = new PdfDocumentRenderer { Document = document };
            renderer.RenderDocument();

            using var stream = new MemoryStream();
            renderer.PdfDocument.Save(stream, false);
            return Task.FromResult<Returning<byte[]>>(stream.ToArray());
        }, saveLog: true);

    private void WriteCover(Document document, PdfExportDocument export)
    {
        var section = document.AddSection();
        ApplyPageSetup(section);

        var spacer = section.AddParagraph();
        spacer.Format.SpaceBefore = "6cm";

        var title = section.AddParagraph(export.Title);
        title.Format.Font.Size = _options.FontSize + 16;
        title.Format.Font.Bold = true;
        title.Format.Alignment = ParagraphAlignment.Center;
        title.Format.SpaceAfter = "1cm";

        var date = section.AddParagraph(export.GeneratedAt.ToString("dd/MM/yyyy HH:mm"));
        date.Format.Alignment = ParagraphAlignment.Center;
        date.Format.Font.Color = Color.Parse("#666666");

        if (export.GeneratedBy is { } author)
        {
            var by = section.AddParagraph(author);
            by.Format.Alignment = ParagraphAlignment.Center;
            by.Format.Font.Color = Color.Parse("#666666");
        }
    }

    private void WriteToc(Document document, PdfExportDocument export, Dictionary<Guid, string> bookmarks)
    {
        var section = _options.IncludeCover ? document.AddSection() : document.AddSection();
        ApplyPageSetup(section);

        var heading = section.AddParagraph("Contenido");
        heading.Format.Font.Size = _options.FontSize + 6;
        heading.Format.Font.Bold = true;
        heading.Format.SpaceAfter = "0.5cm";

        foreach (var item in export.Sections)
        {
            var paragraph = section.AddParagraph();
            paragraph.Style = "KhToc";
            paragraph.Format.LeftIndent = Unit.FromCentimeter(0.5 * (item.Level - 1));

            var link = paragraph.AddHyperlink(bookmarks[item.PagePk]);
            link.AddText(item.Title);
            link.AddTab();
            link.AddPageRefField(bookmarks[item.PagePk]);   // resolved when the document is rendered
        }
    }

    private void WriteBody(Document document, PdfExportDocument export, Dictionary<Guid, string> bookmarks)
    {
        var section = document.AddSection();
        ApplyPageSetup(section);

        if (_options.IncludePageNumbers)
        {
            var footer = section.Footers.Primary.AddParagraph();
            footer.Format.Alignment = ParagraphAlignment.Center;
            footer.Format.Font.Size = _options.FontSize - 2;
            footer.Format.Font.Color = Color.Parse("#666666");
            footer.AddText("Página ");
            footer.AddPageField();
            footer.AddText(" de ");
            footer.AddNumPagesField();
        }

        var first = true;
        foreach (var item in export.Sections)
        {
            if (!first) section.AddPageBreak();
            first = false;

            var heading = section.AddParagraph();
            heading.Style = $"Heading{Math.Clamp(item.Level, 1, 6)}";
            heading.AddBookmark(bookmarks[item.PagePk]);
            heading.AddText(item.Title);

            var body = Parser.ParseDocument(item.ContentHtml).Body;
            if (body is null) continue;

            // Headings inside the content are pushed below the page's own heading so the outline
            // nests instead of every page starting a new top-level branch.
            new HtmlToMigraDoc(section, export.Images, _options, item.Level).Write(body);
        }
    }

    private static void ApplyPageSetup(Section section)
    {
        section.PageSetup.PageFormat = PageFormat.A4;

        // PageFormat alone does NOT fill PageWidth/PageHeight — they stay at 0 until MigraDoc
        // resolves the format at render time. Anything that measures the page before then (column
        // widths, image caps) would compute a NEGATIVE width and lay out garbage, so the size is
        // stated explicitly here as well.
        section.PageSetup.PageWidth = "21cm";
        section.PageSetup.PageHeight = "29.7cm";

        section.PageSetup.TopMargin = "2cm";
        section.PageSetup.BottomMargin = "2cm";
        section.PageSetup.LeftMargin = "2.2cm";
        section.PageSetup.RightMargin = "2.2cm";
    }
}
