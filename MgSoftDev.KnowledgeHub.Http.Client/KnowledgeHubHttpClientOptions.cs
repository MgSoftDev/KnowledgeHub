namespace MgSoftDev.KnowledgeHub.Http.Client;

/// <summary>Configuration of the KnowledgeHub HTTP client services.</summary>
public sealed class KnowledgeHubHttpClientOptions
{
    /// <summary>Base path of the API group on the server. Must match MapKnowledgeHubApi's pattern.</summary>
    public string ApiBasePath { get; set; } = "/kh/api";
}
