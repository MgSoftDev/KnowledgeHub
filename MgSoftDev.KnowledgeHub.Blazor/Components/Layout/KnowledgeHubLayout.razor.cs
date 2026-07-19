using MgSoftDev.KnowledgeHub.Blazor.Components.Embedded;
using Microsoft.AspNetCore.Components;

namespace MgSoftDev.KnowledgeHub.Blazor.Components.Layout;

/// <summary>
/// Two-column shell for the standalone-portal scenario (the whole app IS the documentation).
/// The sidebar is delegated to <see cref="KnowledgeHubNavTree"/>; this layout only adds the
/// chrome and the host's footer links. To embed the module inside an existing application
/// layout, do NOT use this layout — use the components under Components/Embedded.
/// </summary>
public partial class KnowledgeHubLayout : LayoutComponentBase
{
    [Inject] private NavigationManager Nav { get; set; } = null!;

    /// <summary>The sidebar tree, exposed so the layout can refresh it.</summary>
    protected KnowledgeHubNavTree? NavTree { get; set; }

    protected override void OnInitialized()
    {
#if DEBUG
        // Dev nicety: KNOWLEDGEHUB_STARTPAGE=/ruta opens that route on startup. The slug form
        // is resolved by the tree component once it has loaded (see OnAfterRenderAsync).
        var start = Environment.GetEnvironmentVariable("KNOWLEDGEHUB_STARTPAGE");
        if (!string.IsNullOrWhiteSpace(start) && start.StartsWith('/'))
            Nav.NavigateTo(start);
#endif
    }

#if DEBUG
    private bool _startPageHandled;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender || _startPageHandled) return;
        _startPageHandled = true;

        var start = Environment.GetEnvironmentVariable("KNOWLEDGEHUB_STARTPAGE");
        if (string.IsNullOrWhiteSpace(start) || start.StartsWith('/') || NavTree is null) return;

        // Slug form: resolve it against the loaded tree.
        var node = KnowledgeHubNavTree.FindBySlug(NavTree.CurrentRoots, start);
        if (node is not null) Nav.NavigateTo(KnowledgeHubRoutes.Page(node.Pk));

        await Task.CompletedTask;
    }
#endif
}
