namespace MgSoftDev.KnowledgeHub.Store;

/// <summary>Result of an atomic publish attempt (see IKnowledgeHubStore.TryPublishAsync).</summary>
public enum PublishOutcome
{
    Published = 0,

    /// <summary>Another user published a version newer than the caller's baseline.</summary>
    Conflict = 1,

    PageNotFound = 2,

    /// <summary>The page has no versions to publish.</summary>
    NothingToPublish = 3
}
