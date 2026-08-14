using MgSoftDev.KnowledgeHub.Blazor.EditorTools;
using MgSoftDev.KnowledgeHub.Contracts;

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

    /// <summary>
    /// Replaces the module's welcome screen with a component of your own. It covers both places
    /// that screen shows up: the <c>/kh</c> route in portal mode and the empty state of
    /// <c>KnowledgeHubBrowser</c> when no page is selected (there, an explicit
    /// <c>EmptyContent</c> still wins).
    ///
    /// This is the supported way to do it: declaring your own <c>@page "/kh"</c> makes the Router
    /// fail at startup with "The following routes are ambiguous", because the module's page
    /// already claims that route.
    /// </summary>
    public Type? HomeComponent { get; set; }

    /// <summary>
    /// Cleanup level the editor starts on. The user can switch it from the toolbar; this is just
    /// the starting point of each app session. Only affects pasting and the manual cleanup button
    /// — saving always uses the host's sanitizer as-is.
    /// </summary>
    public HtmlCleanupLevel DefaultCleanupLevel { get; set; } = HtmlCleanupLevel.Standard;

    /// <summary>
    /// Starting width of the navigation tree column, in any CSS length the splitter accepts
    /// (<c>"320px"</c>, <c>"25%"</c>). It is only the starting point: the user can drag it, and
    /// unless <see cref="TreeWidthStorageKey"/> is cleared the dragged width wins on the next
    /// visit. <c>KnowledgeHubBrowser</c> can override it per instance.
    /// </summary>
    public string TreeSize { get; set; } = "320px";

    /// <summary>How narrow the tree can be dragged.</summary>
    public string TreeMinSize { get; set; } = "200px";

    /// <summary>How wide the tree can be dragged.</summary>
    public string TreeMaxSize { get; set; } = "60%";

    /// <summary>
    /// Show the arrows on the divider that collapse/expand the tree with one click. Off by
    /// default: they add two clickable targets to a bar whose job is to be dragged, and hitting
    /// one by accident makes a column vanish. The tree can still be dragged all the way to its
    /// minimum.
    /// </summary>
    public bool TreeCollapsible { get; set; }

    /// <summary>
    /// localStorage key under which the dragged tree width is remembered. Set it to null or empty
    /// to stop remembering (the tree then always starts at <see cref="TreeSize"/>). Change it if
    /// the same origin hosts two modules whose trees should size independently.
    /// </summary>
    public string? TreeWidthStorageKey { get; set; } = "kh.treeWidth";

    /// <summary>
    /// localStorage key under which the branches the user collapsed are remembered, so managing a
    /// page no longer reopens the whole tree. Set it to null or empty to stop remembering (the tree
    /// then starts fully expanded on every visit, as it did before). Change it if the same origin
    /// hosts two modules whose trees should be remembered independently.
    /// </summary>
    public string? TreeExpansionStorageKey { get; set; } = "kh.treeCollapsed";
}
