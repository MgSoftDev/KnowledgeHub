using MgSoftDev.KnowledgeHub.Dtos;
using MgSoftDev.ReturningCore;

namespace MgSoftDev.KnowledgeHub.Contracts;

/// <summary>
/// Exports published pages as a downloadable file. This is where ALL the security lives: it checks
/// the export capability once, and then walks the pages one by one through the same visibility
/// filter the reader uses — so an export can never contain a page its owner could not open.
///
/// It is split from <see cref="IKnowledgeHubPdfRenderer"/> on purpose: a host that wants another
/// engine replaces only the renderer and inherits this filtering untouched.
/// </summary>
public interface IKnowledgeHubPdfExportService
{
    /// <summary>
    /// Collects the pages and their images without rendering anything. Useful for a host writing
    /// its own renderer, or to count what an export would contain before paying for it.
    /// </summary>
    /// <param name="rootPagePk">Page to export.</param>
    /// <param name="includeDescendants">Also include every visible published page beneath it.</param>
    Task<Returning<PdfExportDocument>> BuildAsync(Guid rootPagePk, bool includeDescendants);

    /// <summary>
    /// Builds the document and renders it. Returns Unfinished — not an error — when the user
    /// cannot export, the page is not visible or not published, the branch exceeds
    /// <c>KnowledgeHubOptions.MaxExportPages</c>, or no renderer is registered.
    /// </summary>
    Task<Returning<PdfFileDto>> ExportAsync(Guid rootPagePk, bool includeDescendants);
}
