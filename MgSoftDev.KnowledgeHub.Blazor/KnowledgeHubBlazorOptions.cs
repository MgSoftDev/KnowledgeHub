using MgSoftDev.KnowledgeHub.Blazor.EditorTools;

namespace MgSoftDev.KnowledgeHub.Blazor;

/// <summary>UI options of the KnowledgeHub module, configured by the host at registration time.</summary>
public sealed class KnowledgeHubBlazorOptions
{
    /// <summary>Title shown at the top of the navigation sidebar.</summary>
    public string PortalTitle { get; set; } = "📚 KnowledgeHub";

    /// <summary>
    /// Custom tools of the HTML editor toolbar. Pre-populated with the 4 built-in callouts
    /// (see <see cref="BuiltInEditorTools"/>); hosts may Add / Remove / Clear freely.
    /// </summary>
    public List<EditorToolDescriptor> EditorTools { get; } = BuiltInEditorTools.CreateDefaults();

    /// <summary>
    /// Optional host component rendered in the sidebar footer (e.g. a logout button or links
    /// to host pages). Must be a Blazor component type.
    /// </summary>
    public Type? HeaderActionsComponent { get; set; }
}
