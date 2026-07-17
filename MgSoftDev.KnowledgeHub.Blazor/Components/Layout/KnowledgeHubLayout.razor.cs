using MgSoftDev.KnowledgeHub.Blazor.Helpers;
using MgSoftDev.KnowledgeHub.Contracts;
using MgSoftDev.KnowledgeHub.Dtos;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Radzen;

namespace MgSoftDev.KnowledgeHub.Blazor.Components.Layout;

public partial class KnowledgeHubLayout : LayoutComponentBase
{
    [Inject] private IKnowledgeHubPageService DocService { get; set; } = null!;
    [Inject] private IKnowledgeHubUserContext User { get; set; } = null!;
    [Inject] private KnowledgeHubBlazorOptions Options { get; set; } = null!;
    [Inject] private NavigationManager Nav { get; set; } = null!;
    [Inject] private NotificationService Notify { get; set; } = null!;

    protected List<PageTreeNodeDto> Roots { get; private set; } = new();
    protected bool Loading { get; private set; } = true;
    public bool Wait { get; private set; }
    protected string SearchTerm { get; set; } = string.Empty;

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
            Nav.NavigateTo(KnowledgeHubRoutes.Edit(result.Value));
        }
        else
        {
            result.SendNotifyIfNotOk(Notify, "Error al crear la página");
        }
        StateHasChanged();
    }

    private void OnSearchKeyDown(KeyboardEventArgs e)
    {
        if (e.Key == "Enter" && !string.IsNullOrWhiteSpace(SearchTerm))
            Nav.NavigateTo(KnowledgeHubRoutes.Search(SearchTerm.Trim()));
    }

    protected override async Task OnInitializedAsync()
    {
        await LoadTreeAsync();
#if DEBUG
        // Dev nicety: KNOWLEDGEHUB_STARTPAGE=<slug> or =/route opens that page on startup.
        var start = Environment.GetEnvironmentVariable("KNOWLEDGEHUB_STARTPAGE");
        if (!string.IsNullOrWhiteSpace(start))
        {
            if (start.StartsWith('/'))
            {
                Nav.NavigateTo(start);
            }
            else
            {
                var node = FindBySlug(Roots, start);
                if (node is not null) Nav.NavigateTo(KnowledgeHubRoutes.Page(node.Pk));
            }
        }
#endif
    }

    private static PageTreeNodeDto? FindBySlug(IEnumerable<PageTreeNodeDto> nodes, string slug)
    {
        foreach (var node in nodes)
        {
            if (string.Equals(node.Slug, slug, StringComparison.OrdinalIgnoreCase)) return node;
            var found = FindBySlug(node.Children, slug);
            if (found is not null) return found;
        }
        return null;
    }

    private async Task LoadTreeAsync()
    {
        Loading = true;
        var result = await DocService.GetTreeAsync();
        Roots = result.OkNotNull ? result.Value : new List<PageTreeNodeDto>();
        Loading = false;
    }

    private async Task ReloadTreeAsync()
    {
        await LoadTreeAsync();
        StateHasChanged();
    }

    private void OnNodeSelect(TreeEventArgs args)
    {
        if (args.Value is PageTreeNodeDto node)
            Nav.NavigateTo(KnowledgeHubRoutes.Page(node.Pk));
    }
}
