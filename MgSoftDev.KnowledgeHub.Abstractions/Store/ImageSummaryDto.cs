namespace MgSoftDev.KnowledgeHub.Store;

/// <summary>
/// Metadata of one stored image WITHOUT its binary, for maintenance listings (orphan analysis).
/// Implementations must not materialize the image content to build this.
/// </summary>
public sealed record ImageSummaryDto(Guid Pk, string FileName, long SizeBytes);
