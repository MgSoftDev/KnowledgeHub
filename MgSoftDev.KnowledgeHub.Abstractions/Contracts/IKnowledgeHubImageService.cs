using MgSoftDev.KnowledgeHub.Dtos;
using MgSoftDev.ReturningCore;

namespace MgSoftDev.KnowledgeHub.Contracts;

/// <summary>Image ingestion: convert to WebP, resize, hash, deduplicate and persist.</summary>
public interface IKnowledgeHubImageService
{
    /// <summary>
    /// Converts <paramref name="originalBytes"/> to WebP (width capped), computes its SHA-256,
    /// and persists metadata + binary. If an image with the same hash already exists it is
    /// reused instead of duplicated. Returns the DocImage.Pk to embed as <c>docimg://{pk}</c>.
    /// </summary>
    Task<Returning<Guid>> UploadOrReplaceAsync(byte[] originalBytes, string fileName);

    /// <summary>
    /// Counts the images no version references any more, and how much space they take. Read-only:
    /// nothing is deleted. Admin only.
    /// </summary>
    Task<Returning<OrphanImageReportDto>> AnalyzeOrphanImagesAsync();

    /// <summary>
    /// PERMANENTLY deletes the orphan images (metadata, binary and any leftover page links) and
    /// returns how many were removed. Re-runs the analysis first, so a page saved between the
    /// analysis and the deletion cannot lose its images. Admin only.
    /// </summary>
    Task<Returning<int>> DeleteOrphanImagesAsync();
}
