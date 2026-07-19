using Microsoft.AspNetCore.Components;

namespace MgSoftDev.KnowledgeHub.Blazor.Components.Embedded;

/// <summary>
/// Turnkey embeddable portal: navigation tree + content pane, wired together with INTERNAL
/// navigation (it never changes the URL nor takes the user out of your page). Drop it into a
/// page of your host app, inside your own layout:
///
/// <code>
/// @page "/documentacion"
/// &lt;KnowledgeHubBrowser /&gt;
/// </code>
///
/// You can also drive the selection from outside (e.g. from your own menu or a route
/// parameter) by binding <see cref="SelectedPagePk"/>.
/// </summary>
public partial class KnowledgeHubBrowser : ComponentBase
{
    /// <summary>Which screen the content pane is showing.</summary>
    public enum BrowserView
    {
        Empty,
        Page,
        Edit,
        History,
        Version,
        Permissions,
        Manage,
        Search
    }

    private KnowledgeHubNavTree? _navTree;

    /// <summary>Title shown in the tree header and the empty state.</summary>
    [Parameter] public string Title { get; set; } = "Documentación";

    /// <summary>Render the navigation tree column. Set false if your app provides its own menu.</summary>
    [Parameter] public bool ShowTree { get; set; } = true;

    /// <summary>Show the tree header (title + new/refresh). Default true.</summary>
    [Parameter] public bool ShowTreeHeader { get; set; } = true;

    /// <summary>Show the search box inside the tree. Default true.</summary>
    [Parameter] public bool ShowSearch { get; set; } = true;

    /// <summary>Show the signed-in user row inside the tree. Default true.</summary>
    [Parameter] public bool ShowUser { get; set; } = true;

    /// <summary>Allow creating root pages from the tree header (still requires the Edit permission).</summary>
    [Parameter] public bool AllowCreate { get; set; } = true;

    /// <summary>
    /// When true (default) the browser sizes itself to its container (100%) instead of the
    /// viewport, which is what you want inside a host layout with a topbar.
    /// </summary>
    [Parameter] public bool Embedded { get; set; } = true;

    /// <summary>Content shown when no page is selected. Defaults to a short hint.</summary>
    [Parameter] public RenderFragment? EmptyContent { get; set; }

    /// <summary>
    /// Optional content rendered at the bottom of the navigation tree (your own links/actions).
    /// It composes with <see cref="KnowledgeHubBlazorOptions.HeaderActionsComponent"/>, which the
    /// tree renders right after this content.
    /// </summary>
    [Parameter] public RenderFragment? TreeFooterContent { get; set; }

    /// <summary>Currently selected page. Bindable, so the host can drive/observe the selection.</summary>
    [Parameter] public Guid? SelectedPagePk { get; set; }

    [Parameter] public EventCallback<Guid?> SelectedPagePkChanged { get; set; }

    protected BrowserView View { get; private set; } = BrowserView.Empty;
    protected Guid? SelectedVersionPk { get; private set; }
    protected string? SearchTerm { get; private set; }

    private Guid? _lastAppliedSelection;

    protected override void OnParametersSet()
    {
        // Honor an externally-driven selection (host binding / route parameter).
        if (SelectedPagePk == _lastAppliedSelection) return;

        _lastAppliedSelection = SelectedPagePk;
        View = SelectedPagePk is null ? BrowserView.Empty : BrowserView.Page;
    }

    /// <summary>Reloads the navigation tree (e.g. after the host changed content elsewhere).</summary>
    public Task RefreshTreeAsync() => _navTree?.RefreshAsync() ?? Task.CompletedTask;

    /// <summary>Selects a page and shows it in the content pane.</summary>
    public async Task SelectPageAsync(Guid pagePk)
    {
        SelectedPagePk = pagePk;
        _lastAppliedSelection = pagePk;
        View = BrowserView.Page;
        await SelectedPagePkChanged.InvokeAsync(pagePk);
        StateHasChanged();
    }

    private async Task ClearSelectionAsync()
    {
        SelectedPagePk = null;
        _lastAppliedSelection = null;
        View = BrowserView.Empty;
        await SelectedPagePkChanged.InvokeAsync(null);
        await RefreshTreeAsync();
        StateHasChanged();
    }

    private async Task EditPageAsync(Guid pagePk)
    {
        SelectedPagePk = pagePk;
        _lastAppliedSelection = pagePk;
        View = BrowserView.Edit;
        await SelectedPagePkChanged.InvokeAsync(pagePk);
        await RefreshTreeAsync();
        StateHasChanged();
    }

    private Task ShowHistoryAsync(Guid pagePk) => SwitchAsync(pagePk, BrowserView.History);

    private Task ShowPermissionsAsync(Guid pagePk) => SwitchAsync(pagePk, BrowserView.Permissions);

    private Task ShowManageAsync(Guid pagePk) => SwitchAsync(pagePk, BrowserView.Manage);

    private Task SwitchAsync(Guid pagePk, BrowserView view)
    {
        SelectedPagePk = pagePk;
        _lastAppliedSelection = pagePk;
        View = view;
        StateHasChanged();
        return Task.CompletedTask;
    }

    private Task ShowVersionAsync(Guid versionPk)
    {
        SelectedVersionPk = versionPk;
        View = BrowserView.Version;
        StateHasChanged();
        return Task.CompletedTask;
    }

    private Task SearchAsync(string term)
    {
        SearchTerm = term;
        View = BrowserView.Search;
        StateHasChanged();
        return Task.CompletedTask;
    }
}
