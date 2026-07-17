namespace MgSoftDev.KnowledgeHub.Store;

/// <summary>
/// A search hit as produced by the store: full published HTML included so the core can
/// build the snippet.
/// </summary>
public sealed record SearchCandidateDto(Guid PagePk, string Title, string Slug, string? ContentHtml);
