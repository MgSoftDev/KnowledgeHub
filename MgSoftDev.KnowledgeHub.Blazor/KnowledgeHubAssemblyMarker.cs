namespace MgSoftDev.KnowledgeHub.Blazor;

/// <summary>
/// Marker for router registration. The RCL brings no router of its own; the HOST adds
/// <c>AdditionalAssemblies="new[] { typeof(KnowledgeHubAssemblyMarker).Assembly }"</c>
/// to its <c>&lt;Router&gt;</c> so the /kh/* pages resolve.
/// </summary>
public sealed class KnowledgeHubAssemblyMarker
{
    private KnowledgeHubAssemblyMarker() { }
}
