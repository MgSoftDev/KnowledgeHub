using System.Net;
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
    [Inject] private ContextMenuService ContextMenu { get; set; } = null!;

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

    /// <summary>Right-click menu on the nodes. Null follows KnowledgeHubBlazorOptions.TreeContextMenu.</summary>
    [Parameter] public bool? ShowContextMenu { get; set; }

    /// <summary>Raised from the context menu. Without a handler, navigates to /kh/edit/{pk}.</summary>
    [Parameter] public EventCallback<Guid> OnEditRequested { get; set; }

    /// <summary>Raised from the context menu. Without a handler, navigates to /kh/manage/{pk}.</summary>
    [Parameter] public EventCallback<Guid> OnManageRequested { get; set; }

    protected List<PageTreeNodeDto> Roots { get; private set; } = new();
    protected bool Loading { get; private set; } = true;
    public bool Wait { get; private set; }
    protected string SearchTerm { get; set; } = string.Empty;

    /// <summary>
    /// The page the tree considers open, whoever answered: the host through CurrentPagePk, or the
    /// URL in routed mode, where the layout holds the tree and cannot see the route parameter of
    /// the page inside @Body. Highlight, branch opening and the echo guard all read THIS, never
    /// CurrentPagePk: two of them used to and the tree simply never highlighted anything in the
    /// portal (see gotcha 35).
    /// </summary>
    protected Guid? ActivePagePk { get; private set; }

    /// <summary>
    /// That same page as a node of the loaded tree, bound to RadzenTree.Value. It is NOT a spare
    /// copy of the Selected predicate: setting Value to null is the ONLY way to clear
    /// RadzenTree.SelectedItem, which otherwise keeps pointing at the last item selected — and
    /// Radzen swallows the next click on that item (SelectItem skips Change when the item already
    /// is the selected one), so it stops navigating. See gotcha 35.
    /// </summary>
    protected PageTreeNodeDto? ActiveNode { get; private set; }

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
        // Assigned BEFORE the early return: the branch only has to be opened once per page, but
        // the highlight has to be right on every pass.
        ActivePagePk = CurrentPagePk ?? PageFromUrl();
        RefreshActiveNode();
        if (ActivePagePk is not Guid pagePk || _openedBranchFor == pagePk) return;

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
            if (ActivePagePk is Guid pagePk) OpenAncestorsOf(pagePk);

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

    private void RefreshActiveNode() =>
        ActiveNode = ActivePagePk is Guid pagePk ? FindNode(Roots, pagePk) : null;

    private static PageTreeNodeDto? FindNode(IEnumerable<PageTreeNodeDto> nodes, Guid pagePk)
    {
        foreach (var node in nodes)
        {
            if (node.Pk == pagePk) return node;
            var found = FindNode(node.Children, pagePk);
            if (found is not null) return found;
        }
        return null;
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
        // The DTOs are new instances, so the node held from the previous load is now a stranger.
        RefreshActiveNode();
        Loading = false;
    }

    private async Task OnNodeSelect(TreeEventArgs args)
    {
        if (args.Value is not PageTreeNodeDto node) return;

        // Re-applying the highlight — after a reload, or after navigating — makes RadzenTree raise
        // Change exactly as if the user had clicked. Reporting that as a selection dragged the host
        // back to the reader: pressing Edit switched to the editor, the tree refreshed, and the echo
        // pulled the view straight back to the page — with no error anywhere. An echo names the page
        // that is ALREADY the active one, so that is the one case to ignore.
        // Cost: clicking the node that is already active does nothing, so you leave Manage/Edit
        // through their own Back button. Radzen already behaves that way while the tree is not
        // reloaded (SelectItem skips Change when the item is already the selected one).
        if (node.Pk == ActivePagePk) return;

        if (OnPageSelected.HasDelegate) await OnPageSelected.InvokeAsync(node.Pk);
        else Nav.NavigateTo(KnowledgeHubRoutes.Page(node.Pk));
    }

    // ---------------------------------------------------------------- menú contextual

    private const string MenuCopyPath = "copy-path";
    private const string MenuCopyLink = "copy-link";
    private const string MenuNewChild = "new-child";
    private const string MenuEdit = "edit";
    private const string MenuManage = "manage";

    protected bool ContextMenuEnabled => ShowContextMenu ?? Options.TreeContextMenu;

    /// <summary>
    /// Right-click menu of a node. Its reason to exist is "Copiar ruta": writing a link to another
    /// page means getting hold of its route, and the route carries a Guid nobody is going to type.
    ///
    /// It needs <c>&lt;RadzenComponents /&gt;</c> mounted by the host, like the module's dialogs and
    /// notifications already do. Without it Radzen's service does nothing at all — no exception —
    /// so <see cref="ContextMenuEnabled"/> exists to turn the menu off rather than leave a
    /// right-click that looks broken.
    /// </summary>
    private void OnItemContextMenu(TreeItemContextMenuEventArgs args)
    {
        if (!ContextMenuEnabled || args.Value is not PageTreeNodeDto node) return;

        var items = new List<ContextMenuItem>
        {
            new() { Text = "Copiar ruta", Value = MenuCopyPath, Icon = "link" },
            new() { Text = "Copiar enlace", Value = MenuCopyLink, Icon = "add_link" }
        };

        if (User.CanEdit())
        {
            items.Add(new ContextMenuItem { Text = "Nueva página", Value = MenuNewChild, Icon = "add" });
            items.Add(new ContextMenuItem { Text = "Editar", Value = MenuEdit, Icon = "edit" });
            items.Add(new ContextMenuItem { Text = "Gestionar", Value = MenuManage, Icon = "settings" });
        }

        // El callback de Radzen es SÍNCRONO y todo lo que hay debajo es async (portapapeles, crear
        // una página). Se marshala con InvokeAsync por la gotcha 11.
        ContextMenu.Open(args, items, e => _ = InvokeAsync(() => RunMenuActionAsync(e, node)));
    }

    private async Task RunMenuActionAsync(MenuItemEventArgs args, PageTreeNodeDto node)
    {
        ContextMenu.Close();

        switch (args.Value as string)
        {
            case MenuCopyPath:
                await CopyToClipboardAsync(KnowledgeHubRoutes.Page(node.Pk), "Ruta copiada");
                break;

            case MenuCopyLink:
                // El título va escapado: puede llevar & o <, y aquí se está montando HTML que el
                // autor va a pegar tal cual en la vista de código del editor.
                await CopyToClipboardAsync(
                    $"<a href=\"{KnowledgeHubRoutes.Page(node.Pk)}\">{WebUtility.HtmlEncode(node.Title)}</a>",
                    "Enlace copiado");
                break;

            case MenuNewChild:
                await CreateChildPageAsync(node);
                break;

            case MenuEdit:
                if (OnEditRequested.HasDelegate) await OnEditRequested.InvokeAsync(node.Pk);
                else Nav.NavigateTo(KnowledgeHubRoutes.Edit(node.Pk));
                break;

            case MenuManage:
                if (OnManageRequested.HasDelegate) await OnManageRequested.InvokeAsync(node.Pk);
                else Nav.NavigateTo(KnowledgeHubRoutes.Manage(node.Pk));
                break;
        }
    }

    /// <summary>
    /// Copies, and says so. When the clipboard is unavailable —it needs a secure context, which the
    /// WPF virtual host and a plain-http LAN are not— the text is shown in the notification instead
    /// of pretending it worked: the reader can still select it by hand.
    /// </summary>
    private async Task CopyToClipboardAsync(string text, string doneTitle)
    {
        var copied = false;
        try
        {
            var module = await GetModuleAsync();
            copied = await module.InvokeAsync<bool>("copyText", text);
        }
        catch (JSException)
        {
        }
        catch (InvalidOperationException)
        {
        }

        Notify.Notify(new NotificationMessage
        {
            Severity = copied ? NotificationSeverity.Success : NotificationSeverity.Info,
            Summary = copied ? doneTitle : "Cópialo a mano",
            Detail = text,
            Duration = copied ? 2500 : 8000
        });
    }

    /// <summary>Creates a subpage and opens its editor, where the title is renamed for real.</summary>
    private async Task CreateChildPageAsync(PageTreeNodeDto parent)
    {
        Wait = true;
        StateHasChanged();
        var result = await DocService.CreatePageAsync(parent.Pk, "Nueva página");
        Wait = false;

        if (result.OkNotNull)
        {
            // La página nace DENTRO del padre: si esa rama estaba cerrada, el usuario no vería
            // aparecer nada y parecería que no se creó.
            if (UiState.CollapsedPages.Remove(parent.Pk)) _ = PersistCollapsedAsync();

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
