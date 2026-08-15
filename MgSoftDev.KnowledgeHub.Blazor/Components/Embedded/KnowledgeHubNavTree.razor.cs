using System.Text.Json;
using MgSoftDev.KnowledgeHub.Blazor.Helpers;
using MgSoftDev.KnowledgeHub.Contracts;
using MgSoftDev.KnowledgeHub.Dtos;
using MgSoftDev.KnowledgeHub.Security;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using Radzen;

namespace MgSoftDev.KnowledgeHub.Blazor.Components.Embedded;

/// <summary>
/// Navigation tree of the documentation portal, embeddable anywhere (your own sidebar, a
/// drawer, a panel). Loads the permission-filtered tree on init.
///
/// Navigation model: when a callback is supplied the component delegates the action to the
/// host; otherwise it falls back to URL navigation over the built-in /kh routes.
/// </summary>
public partial class KnowledgeHubNavTree : ComponentBase, IDisposable, IAsyncDisposable
{
    [Inject] private IJSRuntime JS { get; set; } = null!;

    [Inject] private IKnowledgeHubPageService DocService { get; set; } = null!;
    [Inject] private IKnowledgeHubUserContext User { get; set; } = null!;
    [Inject] private KnowledgeHubBlazorOptions Options { get; set; } = null!;
    [Inject] private NavigationManager Nav { get; set; } = null!;
    [Inject] private NotificationService Notify { get; set; } = null!;
    [Inject] private KnowledgeHubUiState UiState { get; set; } = null!;

    /// <summary>Header title. Defaults to KnowledgeHubBlazorOptions.PortalTitle.</summary>
    [Parameter] public string? Title { get; set; }

    /// <summary>Show the header row (title + new/refresh buttons). Default true.</summary>
    [Parameter] public bool ShowHeader { get; set; } = true;

    /// <summary>Show the search box. Default true.</summary>
    [Parameter] public bool ShowSearch { get; set; } = true;

    /// <summary>Show the signed-in user row. Default true.</summary>
    [Parameter] public bool ShowUser { get; set; } = true;

    /// <summary>Allow creating root pages from the header (still requires the Edit permission).</summary>
    [Parameter] public bool AllowCreate { get; set; } = true;

    /// <summary>Optional content rendered at the bottom of the tree (links, actions…).</summary>
    [Parameter] public RenderFragment? FooterContent { get; set; }

    /// <summary>Raised when a page is selected. Without a handler, navigates to /kh/page/{pk}.</summary>
    [Parameter] public EventCallback<Guid> OnPageSelected { get; set; }

    /// <summary>Raised after creating a root page. Without a handler, navigates to /kh/edit/{pk}.</summary>
    [Parameter] public EventCallback<Guid> OnCreatePageRequested { get; set; }

    /// <summary>Raised on Enter in the search box. Without a handler, navigates to /kh/search?q=…</summary>
    [Parameter] public EventCallback<string> OnSearchRequested { get; set; }

    /// <summary>
    /// Page currently open, so the tree can highlight it and open the branch leading to it. Hosts
    /// that navigate internally (the Browser) pass it; in routed mode it is read off the URL.
    /// </summary>
    [Parameter] public Guid? CurrentPagePk { get; set; }

    protected List<PageTreeNodeDto> Roots { get; private set; } = new();
    protected bool Loading { get; private set; } = true;
    public bool Wait { get; private set; }
    protected string SearchTerm { get; set; } = string.Empty;

    private IJSObjectReference? _module;
    private Guid? _openedBranchFor;

    protected override async Task OnInitializedAsync()
    {
        // Keeps the tree in sync when a page is renamed, moved, reordered, re-iconed, created,
        // deleted or published from any other screen of the module.
        UiState.PageTreeChanged += OnPageTreeChanged;
        Nav.LocationChanged += OnLocationChanged;
        await LoadTreeAsync();
    }

    /// <summary>
    /// Opens the branch of whichever page is open, without touching the rest. Arriving through a
    /// deep link to a page inside a branch the user had collapsed would otherwise leave it selected
    /// but out of sight.
    /// </summary>
    protected override void OnParametersSet()
    {
        var current = CurrentPagePk ?? PageFromUrl();
        if (current is not Guid pagePk || _openedBranchFor == pagePk) return;

        _openedBranchFor = pagePk;
        if (OpenAncestorsOf(pagePk)) _ = PersistCollapsedAsync();
    }

    /// <summary>
    /// The notification may come from an AsyncReturningCommand body, which does NOT resume on the
    /// Blazor Dispatcher, so the reload is marshalled with InvokeAsync (see gotcha 11).
    /// </summary>
    private void OnPageTreeChanged() => _ = InvokeAsync(RefreshAsync);

    public void Dispose()
    {
        UiState.PageTreeChanged -= OnPageTreeChanged;
        Nav.LocationChanged -= OnLocationChanged;
    }

    /// <summary>
    /// In routed mode the tree lives in the layout and survives navigation, so no parameter ever
    /// changes when the user opens another page — the URL is the only signal that the current page
    /// moved.
    /// </summary>
    private void OnLocationChanged(object? sender, LocationChangedEventArgs e) =>
        _ = InvokeAsync(() =>
        {
            OnParametersSet();
            StateHasChanged();
        });

    // ---------------------------------------------------------------- expansión recordada

    /// <summary>
    /// Reads back the collapsed branches. It has to happen after the first render: under Blazor
    /// Server the component is prerendered on the server, where there is no browser storage to ask.
    /// </summary>
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender || UiState.CollapsedPagesRestored ||
            string.IsNullOrEmpty(Options.TreeExpansionStorageKey)) return;

        UiState.CollapsedPagesRestored = true;
        try
        {
            var module = await GetModuleAsync();
            var stored = await module.InvokeAsync<string?>("readSetting", Options.TreeExpansionStorageKey);
            if (string.IsNullOrWhiteSpace(stored)) return;

            var pks = JsonSerializer.Deserialize<List<Guid>>(stored);
            if (pks is null || pks.Count == 0) return;

            foreach (var pk in pks) UiState.CollapsedPages.Add(pk);

            // The branch of the page being viewed wins over what was stored, or a deep link into a
            // collapsed branch would restore it closed right after we opened it.
            if ((CurrentPagePk ?? PageFromUrl()) is Guid pagePk) OpenAncestorsOf(pagePk);

            StateHasChanged();
        }
        catch (JsonException)
        {
            // Someone else wrote under our key, or the format changed: start over rather than fail.
        }
        catch (JSException)
        {
            // Storage disabled (private window, some WebView2 origins): the tree still works.
        }
        catch (InvalidOperationException)
        {
            // Prerendering, or the circuit went away mid-call.
        }
    }

    private Task OnNodeExpand(TreeExpandEventArgs args) => ToggleCollapsedAsync(args.Value, collapsed: false);

    private Task OnNodeCollapse(TreeEventArgs args) => ToggleCollapsedAsync(args.Value, collapsed: true);

    private Task ToggleCollapsedAsync(object? value, bool collapsed)
    {
        if (value is not PageTreeNodeDto node) return Task.CompletedTask;

        var changed = collapsed
            ? UiState.CollapsedPages.Add(node.Pk)
            : UiState.CollapsedPages.Remove(node.Pk);

        return changed ? PersistCollapsedAsync() : Task.CompletedTask;
    }

    /// <summary>Clears the collapsed flag off every ancestor of the page. True when something changed.</summary>
    private bool OpenAncestorsOf(Guid pagePk)
    {
        var chain = new List<Guid>();
        if (!FindChain(Roots, pagePk, chain)) return false;

        var changed = false;
        // The page itself stays as the user left it: opening a page says nothing about whether you
        // want to see its subpages.
        foreach (var pk in chain.Where(pk => pk != pagePk))
            changed |= UiState.CollapsedPages.Remove(pk);
        return changed;
    }

    private static bool FindChain(IEnumerable<PageTreeNodeDto> nodes, Guid pagePk, List<Guid> chain)
    {
        foreach (var node in nodes)
        {
            chain.Add(node.Pk);
            if (node.Pk == pagePk || FindChain(node.Children, pagePk, chain)) return true;
            chain.RemoveAt(chain.Count - 1);
        }
        return false;
    }

    /// <summary>Page pk in the current URL, for the routed mode (/kh/page/{pk}, /kh/edit/{pk}…).</summary>
    private Guid? PageFromUrl()
    {
        var path = Nav.ToBaseRelativePath(Nav.Uri);
        var query = path.IndexOf('?');
        if (query >= 0) path = path[..query];

        foreach (var segment in path.Split('/', StringSplitOptions.RemoveEmptyEntries))
            if (Guid.TryParse(segment, out var pk)) return pk;
        return null;
    }

    private async Task PersistCollapsedAsync()
    {
        if (string.IsNullOrEmpty(Options.TreeExpansionStorageKey)) return;

        try
        {
            var module = await GetModuleAsync();
            await module.InvokeAsync<bool>("writeSetting", Options.TreeExpansionStorageKey,
                JsonSerializer.Serialize(UiState.CollapsedPages));
        }
        catch (JSException)
        {
        }
        catch (InvalidOperationException)
        {
        }
    }

    private async Task<IJSObjectReference> GetModuleAsync() =>
        _module ??= await JS.InvokeAsync<IJSObjectReference>(
            "import", "./_content/MgSoftDev.KnowledgeHub.Blazor/knowledgehub.js");

    public async ValueTask DisposeAsync()
    {
        if (_module is null) return;

        try
        {
            await _module.DisposeAsync();
        }
        catch (JSDisconnectedException)
        {
        }
        catch (JSException)
        {
        }

        _module = null;
    }

    /// <summary>Reloads the tree. Public so hosts can refresh after their own changes.</summary>
    public async Task RefreshAsync()
    {
        await LoadTreeAsync();
        StateHasChanged();
    }

    private async Task LoadTreeAsync()
    {
        Loading = true;
        var result = await DocService.GetTreeAsync();
        Roots = result.OkNotNull ? result.Value : new List<PageTreeNodeDto>();
        Loading = false;
    }

    private async Task OnNodeSelect(TreeEventArgs args)
    {
        if (args.Value is not PageTreeNodeDto node) return;

        // Reloading the tree re-applies the highlight, and RadzenTree raises Change for it exactly
        // as if the user had clicked. Reporting that as a selection dragged the host back to the
        // reader: pressing Edit switched to the editor, the tree refreshed, and the echo pulled the
        // view straight back to the page — with no error anywhere. An echo names the page that is
        // ALREADY current, so that is the one case to ignore.
        if (node.Pk == CurrentPagePk) return;

        if (OnPageSelected.HasDelegate) await OnPageSelected.InvokeAsync(node.Pk);
        else Nav.NavigateTo(KnowledgeHubRoutes.Page(node.Pk));
    }

    private async Task CreateRootPageAsync()
    {
        Wait = true;
        StateHasChanged();
        // No slug: the service derives it from the title and suffixes it if taken. This used to
        // pass a random one, which left every root page with a permanent "nueva-pagina-3f8a1c2b".
        var result = await DocService.CreatePageAsync(null, "Nueva página");
        Wait = false;

        if (result.OkNotNull)
        {
            await LoadTreeAsync();
            if (OnCreatePageRequested.HasDelegate) await OnCreatePageRequested.InvokeAsync(result.Value);
            else Nav.NavigateTo(KnowledgeHubRoutes.Edit(result.Value));
        }
        else
        {
            result.SendNotifyIfNotOk(Notify, "Error al crear la página");
        }
        StateHasChanged();
    }

    private async Task OnSearchKeyDown(KeyboardEventArgs e)
    {
        if (e.Key != "Enter" || string.IsNullOrWhiteSpace(SearchTerm)) return;

        var term = SearchTerm.Trim();
        if (OnSearchRequested.HasDelegate) await OnSearchRequested.InvokeAsync(term);
        else Nav.NavigateTo(KnowledgeHubRoutes.Search(term));
    }

    /// <summary>Finds a node by slug across the whole tree (used by hosts and by the layout).</summary>
    public static PageTreeNodeDto? FindBySlug(IEnumerable<PageTreeNodeDto> nodes, string slug)
    {
        foreach (var node in nodes)
        {
            if (string.Equals(node.Slug, slug, StringComparison.OrdinalIgnoreCase)) return node;
            var found = FindBySlug(node.Children, slug);
            if (found is not null) return found;
        }
        return null;
    }

    /// <summary>Current roots, so a host can inspect the loaded tree (e.g. resolve a slug).</summary>
    public IReadOnlyList<PageTreeNodeDto> CurrentRoots => Roots;
}
