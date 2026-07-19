using MgSoftDev.KnowledgeHub.Blazor.Helpers;
using MgSoftDev.KnowledgeHub.Contracts;
using MgSoftDev.KnowledgeHub.Dtos;
using MgSoftDev.KnowledgeHub.Security;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Radzen;

namespace MgSoftDev.KnowledgeHub.Blazor.Components.Embedded;

/// <summary>
/// Navigation tree of the documentation portal, embeddable anywhere (your own sidebar, a
/// drawer, a panel). Loads the permission-filtered tree on init.
///
/// Navigation model: when a callback is supplied the component delegates the action to the
/// host; otherwise it falls back to URL navigation over the built-in /kh routes.
/// </summary>
public partial class KnowledgeHubNavTree : ComponentBase
{
    [Inject] private IKnowledgeHubPageService DocService { get; set; } = null!;
    [Inject] private IKnowledgeHubUserContext User { get; set; } = null!;
    [Inject] private KnowledgeHubBlazorOptions Options { get; set; } = null!;
    [Inject] private NavigationManager Nav { get; set; } = null!;
    [Inject] private NotificationService Notify { get; set; } = null!;

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

    protected List<PageTreeNodeDto> Roots { get; private set; } = new();
    protected bool Loading { get; private set; } = true;
    public bool Wait { get; private set; }
    protected string SearchTerm { get; set; } = string.Empty;

    protected override async Task OnInitializedAsync() => await LoadTreeAsync();

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

        if (OnPageSelected.HasDelegate) await OnPageSelected.InvokeAsync(node.Pk);
        else Nav.NavigateTo(KnowledgeHubRoutes.Page(node.Pk));
    }

    private async Task CreateRootPageAsync()
    {
        Wait = true;
        StateHasChanged();
        var slug = $"nueva-pagina-{Guid.NewGuid():n}"[..24];
        var result = await DocService.CreatePageAsync(null, "Nueva página", slug);
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
