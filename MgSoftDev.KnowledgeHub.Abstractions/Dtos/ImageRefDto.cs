namespace MgSoftDev.KnowledgeHub.Dtos;

/// <summary>
/// Minimal image reference used by the cache flow: the stable logical id (DocImage.Pk,
/// embedded in HTML as docimg://{Pk}) and the content hash that names the cache file.
/// Deliberately excludes the binary.
/// </summary>
public sealed record ImageRefDto(Guid Pk, string ContentHash);
