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
}
