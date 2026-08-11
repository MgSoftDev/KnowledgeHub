using MgSoftDev.KnowledgeHub.Dtos;
using MgSoftDev.ReturningCore;

namespace MgSoftDev.KnowledgeHub.Contracts;

/// <summary>
/// Turns an already-authorized <see cref="PdfExportDocument"/> into bytes. It is the ONLY thing a
/// host has to write to swap the PDF engine — permissions, visibility and image loading stay in
/// <see cref="IKnowledgeHubPdfExportService"/>.
///
/// Optional, like the HTML sanitizer: the library resolves it with <c>GetService&lt;T&gt;()</c>, and
/// without one registered the export button never appears. Install
/// <c>MgSoftDev.KnowledgeHub.Pdf</c> for the default engine, or implement this over Playwright,
/// wkhtmltopdf or whatever your organisation already licenses.
///
/// Implementations must be safe to use as a singleton.
/// </summary>
public interface IKnowledgeHubPdfRenderer
{
    /// <summary>
    /// The document's HTML still carries <c>docimg://{pk}</c> references; resolve them against
    /// <see cref="PdfExportDocument.Images"/>. Images arrive as stored — WebP — so transcode if
    /// your engine cannot read it.
    /// </summary>
    Task<Returning<byte[]>> RenderAsync(PdfExportDocument document);
}
