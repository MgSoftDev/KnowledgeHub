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
}
