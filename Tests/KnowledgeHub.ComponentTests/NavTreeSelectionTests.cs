using Bunit;
using MgSoftDev.KnowledgeHub;
using MgSoftDev.KnowledgeHub.Blazor.Components.Embedded;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace KnowledgeHub.ComponentTests;

/// <summary>
/// Which node the tree marks as the open one. Two modes with two sources of truth: the embedded
/// host hands the page over in CurrentPagePk, and the routed portal has none to hand — its tree
/// lives in the layout, which cannot see the route parameter of the page inside @Body, so the URL
/// is the only signal.
///
/// The portal half of this file was all red before v0.19.4: the highlight read CurrentPagePk alone,
/// so it marked nothing there, and what looked like a selection was Radzen's own click state — which
/// dies on every reload because the tree keys its items by DTO instance.
///
/// Asserted on aria-selected rather than on a Radzen CSS class: it is the accessibility contract of
/// the tree, it says what we actually mean, and it survives Radzen renaming its classes.
/// </summary>
public class NavTreeSelectionTests : ComponentTestBase
{
    /// <summary>Arriving by URL — a deep link, a typed address — marks the page in the tree.</summary>
    [Fact]
    public void Modo_portal_marca_la_pagina_de_la_URL()
    {
        GoTo(KnowledgeHubRoutes.Page(FakePageService.ChildPk));

        var tree = Render<KnowledgeHubNavTree>();

        Assert.Equal("Primeros pasos", SelectedTitle(tree));
    }

    /// <summary>
    /// The mark follows navigation. Also pins WHY the fix needs nothing else: RadzenTreeItem honours
    /// a changed Selected parameter, so a re-render moves the highlight and the tree never goes back
    /// to the store — asserted here, because "it also reloads" would be an invisible round trip per
    /// navigation.
    /// </summary>
    [Fact]
    public async Task Modo_portal_mueve_la_marca_al_navegar()
    {
        GoTo(KnowledgeHubRoutes.Page(FakePageService.RootPk));
        var tree = Render<KnowledgeHubNavTree>();
        Assert.Equal("Manual", SelectedTitle(tree));

        var loadsBefore = Pages.TreeLoads;
        await tree.InvokeAsync(() => GoTo(KnowledgeHubRoutes.Page(FakePageService.ChildPk)));

        Assert.Equal("Primeros pasos", SelectedTitle(tree));
        Assert.Equal(loadsBefore, Pages.TreeLoads);
    }

    /// <summary>
    /// Reloading keeps the mark. This is the reported symptom: pressing the refresh button, and
    /// saving in Permissions/Manage/Editor (all of which refresh the tree), wiped the highlight.
    /// </summary>
    [Fact]
    public async Task Modo_portal_conserva_la_marca_al_recargar()
    {
        GoTo(KnowledgeHubRoutes.Page(FakePageService.ChildPk));
        var tree = Render<KnowledgeHubNavTree>();
        Assert.Equal("Primeros pasos", SelectedTitle(tree));

        var loadsBefore = Pages.TreeLoads;
        await tree.InvokeAsync(() => tree.Instance.RefreshAsync());

        Assert.True(Pages.TreeLoads > loadsBefore, "El árbol no se recargó: el test no prueba nada.");
        Assert.Equal("Primeros pasos", SelectedTitle(tree));
    }

    /// <summary>
    /// The other half of highlighting in routed mode, and the reason the echo guard is not optional:
    /// re-applying the mark makes RadzenTree raise Change as if the user had clicked, and here the
    /// tree navigates on its own. Without the guard, refreshing while editing lands you on the
    /// reader — the v0.19.3 regression, reappearing in the portal.
    /// </summary>
    [Fact]
    public async Task Modo_portal_el_eco_no_saca_del_editor()
    {
        var editing = KnowledgeHubRoutes.Edit(FakePageService.ChildPk);
        GoTo(editing);
        var tree = Render<KnowledgeHubNavTree>();
        Assert.Equal("Primeros pasos", SelectedTitle(tree));

        await tree.InvokeAsync(() => tree.Instance.RefreshAsync());

        Assert.EndsWith(editing, Nav.Uri);
    }

    /// <summary>
    /// Salir de la página y volver a pulsarla en el árbol tiene que navegar. Suena a perogrullada y
    /// no lo es: RadzenTree recuerda su SelectedItem y NO dispara Change al reseleccionarlo, así que
    /// el clic se lo tragaba en silencio. Marcar desde la URL agravó el caso —ahora también
    /// selecciona sin que nadie haga clic—, y se cierra enlazando Value, que es lo único que
    /// limpia ese SelectedItem.
    /// </summary>
    [Fact]
    public async Task Modo_portal_volver_al_inicio_no_deja_el_arbol_mudo()
    {
        var page = KnowledgeHubRoutes.Page(FakePageService.RootPk);
        GoTo(page);
        var tree = Render<KnowledgeHubNavTree>();
        Assert.Equal("Manual", SelectedTitle(tree));

        await tree.InvokeAsync(() => GoTo(KnowledgeHubRoutes.Home));
        Assert.Null(SelectedTitle(tree));

        tree.FindAll(".kh-tree-label").First().Click();

        Assert.EndsWith(page, Nav.Uri);
    }

    /// <summary>Net under the embedded mode, which already worked: the host-supplied page still wins.</summary>
    [Fact]
    public async Task Modo_embebido_conserva_la_marca_al_recargar()
    {
        var tree = Render<KnowledgeHubNavTree>(p =>
            p.Add(t => t.CurrentPagePk, FakePageService.ChildPk));
        Assert.Equal("Primeros pasos", SelectedTitle(tree));

        await tree.InvokeAsync(() => tree.Instance.RefreshAsync());

        Assert.Equal("Primeros pasos", SelectedTitle(tree));
    }

    // ---------------------------------------------------------------- helpers

    private NavigationManager Nav => Services.GetRequiredService<NavigationManager>();

    private void GoTo(string route) => Nav.NavigateTo(route);

    /// <summary>
    /// Title of the marked node, or null when nothing is marked — which is the failure this file
    /// exists for, so it has to be a plain null and not an exception from Find().
    /// </summary>
    private static string? SelectedTitle(IRenderedComponent<KnowledgeHubNavTree> tree) =>
        tree.FindAll("[aria-selected='true']")
            .FirstOrDefault()?
            .QuerySelector(".kh-tree-label")?
            .TextContent.Trim();
}
