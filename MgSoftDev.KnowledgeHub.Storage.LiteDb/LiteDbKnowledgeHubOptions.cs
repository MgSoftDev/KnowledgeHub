namespace MgSoftDev.KnowledgeHub.Storage.LiteDb;

/// <summary>Configuration of the LiteDB storage provider.</summary>
public sealed class LiteDbKnowledgeHubOptions
{
    /// <summary>Absolute or relative path of the .db file. The folder is created if missing.</summary>
    public string DatabasePath { get; set; } = "knowledgehub.db";

    /// <summary>
    /// Prefix applied to every KnowledgeHub collection so the module can live inside a host's
    /// existing LiteDB file without name collisions.
    /// </summary>
    public string CollectionPrefix { get; set; } = "kh_";
}
