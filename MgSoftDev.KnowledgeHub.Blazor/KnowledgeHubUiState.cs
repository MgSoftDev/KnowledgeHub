using MgSoftDev.KnowledgeHub.Contracts;

namespace MgSoftDev.KnowledgeHub.Blazor;

/// <summary>
/// Lightweight in-UI notification bus so the module's components can react to each other without
/// knowing one another. Registered Scoped, which matches the UI lifetime in every hosting model
/// (the root scope in WPF, one per circuit in Blazor Server, ~singleton in WASM).
///
/// A host can also raise these itself after changing pages through its own screens, so the
/// KnowledgeHub tree stays in sync.
/// </summary>
public sealed class KnowledgeHubUiState
{
    /// <summary>
    /// Raised when something that the navigation tree displays changed: title, parent, order,
    /// icon, a new page, a deleted page, or a publish (publishing moves the page title).
    /// Subscribers run on whatever thread raised it — a Blazor component must marshal with
    /// <c>InvokeAsync</c> before touching its state.
    /// </summary>
    public event Action? PageTreeChanged;

    /// <summary>Signals that the tree should reload.</summary>
    public void NotifyPageTreeChanged() => PageTreeChanged?.Invoke();

    /// <summary>
    /// How aggressively pasted content (and the manual cleanup button) is cleaned. Chosen from the
    /// editor toolbar and kept here so it survives navigating between pages; being Scoped, it
    /// resets to the configured default when the app reloads. It never affects saving.
    /// </summary>
    public HtmlCleanupLevel CleanupLevel { get; set; } = HtmlCleanupLevel.Standard;

    /// <summary>
    /// Branches the user has COLLAPSED. Stated in the negative on purpose: the tree has always
    /// opened everything, so an empty set means exactly today's behaviour, a page added later shows
    /// up open like its siblings, and what gets stored stays proportional to what the user actually
    /// changed instead of to the size of the documentation.
    /// <para>
    /// It lives here rather than in the tree component because the component does not survive: the
    /// splitter remounts it on the first render when there is a stored width, and every management
    /// action reloads it. This is the only place in the UI that outlives both.
    /// </para>
    /// </summary>
    public HashSet<Guid> CollapsedPages { get; } = new();

    /// <summary>
    /// True once the collapsed set has been read back from browser storage, so the trip is made
    /// once per session and a second tree instance does not undo what the first restored.
    /// </summary>
    public bool CollapsedPagesRestored { get; set; }
}
