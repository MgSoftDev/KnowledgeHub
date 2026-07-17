namespace MgSoftDev.KnowledgeHub.Store;

/// <summary>Audit values a store writes into the Row* columns of the rows it touches.</summary>
public readonly record struct AuditStamp(string? UserName, DateTime Timestamp);
