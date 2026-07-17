using MgSoftDev.KnowledgeHub.Enums;

namespace MgSoftDev.KnowledgeHub.Store;

/// <summary>Full content snapshot of one page version, as returned by the store.</summary>
public sealed record PageVersionDto(
    Guid VersionPk,
    Guid PagePk,
    int VersionNumber,
    string Title,
    string ContentHtml,
    DocPageStatus Status,
    DateTime? PublishedAt);
