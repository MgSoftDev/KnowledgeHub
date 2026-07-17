namespace MgSoftDev.KnowledgeHub.Store;

/// <summary>Parent link of one page; the core uses these for cycle detection and subtree collection.</summary>
public sealed record PageLinkDto(Guid Pk, Guid? ParentPk);
