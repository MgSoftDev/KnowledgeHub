using Bunit;
using MgSoftDev.KnowledgeHub;
using MgSoftDev.KnowledgeHub.Blazor.Components.Embedded;
using MgSoftDev.KnowledgeHub.Contracts;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace KnowledgeHub.ComponentTests;

/// <summary>
/// Right-click menu of the tree, and the links it exists to produce.
///
/// Radzen's overlay hosts are mounted on purpose (RenderRadzenOverlays): ContextMenuService
/// raises an event and nothing else, so without that host a menu that never opens would pass
/// every assertion. The menu is painted THERE, not inside the tree.
///
/// What stays out of reach here: the clipboard and the click interception itself are JavaScript,
/// and bUnit runs interop in Loose mode. Their C# ends — what gets copied is decided in the same
/// switch as everything else, and where a link ends up — are covered; the browser halves are
/// verified by hand.
/// </summary>
public class TreeContextMenuTests : ComponentTestBase
{
    [Fact]
    public void Clic_derecho_abre_el_menu_con_las_acciones()
    {
        var overlays = RenderRadzenOverlays();
        var tree = Render<KnowledgeHubNavTree>();

        OpenContextMenu(tree);

        var menu = overlays.Markup;
        Assert.Contains("Copiar ruta", menu);
        Assert.Contains("Copiar enlace", menu);
        Assert.Contains("Nueva página", menu);
        Assert.Contains("Editar", menu);
        Assert.Contains("Gestionar", menu);
    }

    /// <summary>
    /// Quien solo puede leer ve las dos de copiar y nada más. Es lo mismo que ya hace el botón de
    /// crear en la cabecera, y el menú no puede ser la puerta de atrás que se lo salte.
    /// </summary>
    [Fact]
    public void Sin_permiso_de_edicion_solo_quedan_las_de_copiar()
    {
        Services.AddScoped<IKnowledgeHubUserContext>(_ => new FakeUser(admin: false));

        var overlays = RenderRadzenOverlays();
        var tree = Render<KnowledgeHubNavTree>();

        OpenContextMenu(tree);

        var menu = overlays.Markup;
        Assert.Contains("Copiar ruta", menu);
        Assert.DoesNotContain("Nueva página", menu);
        Assert.DoesNotContain("Gestionar", menu);
    }

    /// <summary>La salida de emergencia para un anfitrión sin &lt;RadzenComponents /&gt;.</summary>
    [Fact]
    public void Se_puede_apagar()
    {
        var overlays = RenderRadzenOverlays();
        var tree = Render<KnowledgeHubNavTree>(p => p.Add(t => t.ShowContextMenu, false));

        OpenContextMenu(tree);

        Assert.DoesNotContain("Copiar ruta", overlays.Markup);
    }

    // ---------------------------------------------------------------- enlaces entre páginas

    /// <summary>
    /// LA RAZÓN DE SER de todo esto. Un enlace pegado en el contenido apunta a /kh/page/{guid}; en
    /// modo embebido dejar que el navegador lo siga sacaría al usuario de la pantalla del anfitrión,
    /// así que el lector lo resuelve con su propia navegación.
    /// </summary>
    [Fact]
    public async Task Un_enlace_a_otra_pagina_se_resuelve_por_dentro()
    {
        Guid? requested = null;
        var view = Render<KnowledgeHubPageView>(p => p
            .Add(v => v.PagePk, FakePageService.RootPk)
            .Add(v => v.OnPageRequested, EventCallback.Factory.Create<Guid>(this, pk => requested = pk)));

        await view.InvokeAsync(() =>
            view.Instance.OpenPageFromLinkAsync(KnowledgeHubRoutes.Page(FakePageService.ChildPk)));

        Assert.Equal(FakePageService.ChildPk, requested);
        Assert.DoesNotContain("/kh/page/", Nav.Uri);
    }

    /// <summary>Sin anfitrión que lo maneje —modo portal— el mismo enlace navega por URL.</summary>
    [Fact]
    public async Task Sin_manejador_el_enlace_navega_por_la_ruta()
    {
        var view = Render<KnowledgeHubPageView>(p => p.Add(v => v.PagePk, FakePageService.RootPk));

        await view.InvokeAsync(() =>
            view.Instance.OpenPageFromLinkAsync(KnowledgeHubRoutes.Page(FakePageService.ChildPk)));

        Assert.EndsWith(KnowledgeHubRoutes.Page(FakePageService.ChildPk), Nav.Uri);
    }

    /// <summary>
    /// Una ruta que no nombra una página no mueve nada. El navegador ya retuvo el clic cuando esto
    /// corre, así que tragárselo es justo lo que hay que hacer: un enlace roto se queda quieto.
    /// </summary>
    [Fact]
    public async Task Una_ruta_que_no_es_de_una_pagina_no_navega()
    {
        var view = Render<KnowledgeHubPageView>(p => p.Add(v => v.PagePk, FakePageService.RootPk));
        var before = Nav.Uri;

        await view.InvokeAsync(() => view.Instance.OpenPageFromLinkAsync("/kh/page/no-soy-un-guid"));

        Assert.Equal(before, Nav.Uri);
    }

    // ---------------------------------------------------------------- helpers

    private NavigationManager Nav => Services.GetRequiredService<NavigationManager>();

    /// <summary>
    /// Right-clicks the first node. Radzen puts the handler on the &lt;li&gt; of the node and
    /// already suppresses the browser's own menu there, so this is the real path the user takes.
    /// </summary>
    private static void OpenContextMenu(IRenderedComponent<KnowledgeHubNavTree> tree) =>
        tree.FindAll("li.rz-treenode").First().ContextMenu();
}
