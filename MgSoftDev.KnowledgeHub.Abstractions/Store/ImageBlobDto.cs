namespace MgSoftDev.KnowledgeHub.Store;

/// <summary>One image binary as returned by the store's batch blob query.</summary>
public sealed record ImageBlobDto(Guid ImagePk, byte[] Content);
