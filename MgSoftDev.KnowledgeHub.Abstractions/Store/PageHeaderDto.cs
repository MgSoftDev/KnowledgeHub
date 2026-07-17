namespace MgSoftDev.KnowledgeHub.Store;

/// <summary>Lightweight page header used by the read path.</summary>
public sealed record PageHeaderDto(Guid Pk, string Title, Guid? PublishedVersionPk);
