namespace MgSoftDev.KnowledgeHub.Store;

/// <summary>
/// Structural row of one active page. The core uses these for cycle detection, subtree collection
/// and to renumber sibling groups, so it carries the two fields ordering depends on:
/// <see cref="SortOrder"/> and <see cref="Title"/> (the tie-breaker, matching how the tree sorts).
/// </summary>
public sealed record PageLinkDto(Guid Pk, Guid? ParentPk, int SortOrder, string Title);
