namespace MgSoftDev.KnowledgeHub.Contracts;

/// <summary>Maps a content hash to the display URL of the current hosting model.</summary>
public interface IKnowledgeHubImageUrlResolver
{
    /// <summary>
    /// Display URL for the image with the given content hash. The URL MUST end with
    /// <c>{contentHash}.webp</c> — the save path relies on that suffix to normalize display
    /// URLs back to <c>docimg://{pk}</c> regardless of the base address.
    /// </summary>
    string GetImageUrl(string contentHash);
}
