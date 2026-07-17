namespace MgSoftDev.KnowledgeHub.Storage.EntityFramework;

/// <summary>
/// Physical placement of the KnowledgeHub tables inside the host database. One configuration
/// per process (the EF model is cached per context type).
/// </summary>
public sealed class KnowledgeHubEfModelOptions
{
    /// <summary>
    /// Database schema that hosts the KnowledgeHub tables. Default "kh" keeps the module
    /// isolated inside a host database; set null/empty for the provider default (dbo).
    /// </summary>
    public string? Schema { get; set; } = "kh";

    /// <summary>Optional prefix for every table name (e.g. "KH_"), for hosts without schema support.</summary>
    public string TablePrefix { get; set; } = string.Empty;
}
