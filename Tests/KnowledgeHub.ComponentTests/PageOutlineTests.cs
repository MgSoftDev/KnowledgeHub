using Bunit;
using MgSoftDev.KnowledgeHub.Blazor;
using MgSoftDev.KnowledgeHub.Blazor.Components.Embedded;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace KnowledgeHub.ComponentTests;

/// <summary>
/// The "En esta página" panel of <see cref="KnowledgeHubPageView"/>: when it shows up, what it
/// lists, and that folding it holds.
///
/// What is NOT here, and cannot be: jumping and the reading highlight are JavaScript, and bUnit runs
/// with JSInterop in Loose mode — no script executes. Those are verified by hand in the demo. What
/// these tests do lock down is everything that decides WHETHER the panel exists and what it says,
/// which is where a regression would actually hide.
/// </summary>
public class PageOutlineTests : ComponentTestBase
{
    private const string TwoHeadings =
        "<h2>Instalación</h2><p>x</p><h2>Configuración</h2><p>y</p>";

    /// <summary>Two headings is where an index starts being navigation instead of noise.</summary>
    [Fact]
    public void Con_varios_encabezados_aparece_el_panel()
    {
        Pages.ContentHtml = TwoHeadings;

        var view = Render<KnowledgeHubPageView>(p => p.Add(v => v.PagePk, FakePageService.RootPk));

        Assert.Equal(
            ["Instalación", "Configuración"],
            view.FindAll(".kh-doc-toc-link").Select(b => b.TextContent.Trim()));
    }

    /// <summary>
    /// The anchors have to be in the content too, not just in the panel: they are injected on the
    /// way to the screen precisely because the sanitizer strips `id` from anything stored.
    /// </summary>
    [Fact]
    public void El_contenido_sale_con_las_anclas_puestas()
    {
        Pages.ContentHtml = TwoHeadings;

        var view = Render<KnowledgeHubPageView>(p => p.Add(v => v.PagePk, FakePageService.RootPk));

        Assert.Contains("<h2 id=\"kh-instalacion\">", view.Markup);
        Assert.Equal(
            view.FindAll(".kh-doc-toc-link").Select(b => b.GetAttribute("data-kh-anchor")),
            ["kh-instalacion", "kh-configuracion"]);
    }

    [Theory]
    [InlineData("<h2>Solo uno</h2><p>x</p>")]
    [InlineData("<p>Ni un encabezado</p>")]
    public void Sin_nada_que_indexar_no_hay_panel(string html)
    {
        Pages.ContentHtml = html;

        var view = Render<KnowledgeHubPageView>(p => p.Add(v => v.PagePk, FakePageService.RootPk));

        Assert.Empty(view.FindAll(".kh-doc-toc"));
    }

    /// <summary>El anfitrión puede apagarlo del todo, y el parámetro manda sobre las opciones.</summary>
    [Fact]
    public void El_anfitrion_puede_apagarlo()
    {
        Pages.ContentHtml = TwoHeadings;

        var view = Render<KnowledgeHubPageView>(p => p
            .Add(v => v.PagePk, FakePageService.RootPk)
            .Add(v => v.ShowOutline, false));

        Assert.Empty(view.FindAll(".kh-doc-toc"));
    }

    /// <summary>Con maxLevel 2, un h3 deja de listarse — y deja de recibir ancla.</summary>
    [Fact]
    public void El_nivel_maximo_recorta_la_lista()
    {
        Pages.ContentHtml = "<h2>Uno</h2><h3>Hondo</h3><h2>Dos</h2>";

        var view = Render<KnowledgeHubPageView>(p => p
            .Add(v => v.PagePk, FakePageService.RootPk)
            .Add(v => v.OutlineMaxLevel, 2));

        Assert.Equal(["Uno", "Dos"], view.FindAll(".kh-doc-toc-link").Select(b => b.TextContent.Trim()));
        Assert.Contains("<h3>Hondo</h3>", view.Markup);
    }

    /// <summary>
    /// Plegar esconde la lista y deja el botón para recuperarla, y el estado va a UiState — no a un
    /// parámetro: el lector recarga del store en cada set de parámetros, así que plegarlo desde
    /// fuera costaría un viaje a la base de datos por clic.
    /// </summary>
    [Fact]
    public void Plegar_esconde_la_lista_y_se_puede_deshacer()
    {
        Pages.ContentHtml = TwoHeadings;
        var uiState = Services.GetRequiredService<KnowledgeHubUiState>();

        var view = Render<KnowledgeHubPageView>(p => p.Add(v => v.PagePk, FakePageService.RootPk));
        Assert.NotEmpty(view.FindAll(".kh-doc-toc-link"));

        view.Find(".kh-doc-toc-toggle").Click();

        Assert.True(uiState.OutlineCollapsed);
        Assert.Empty(view.FindAll(".kh-doc-toc-link"));
        Assert.NotEmpty(view.FindAll(".kh-doc-toc-toggle"));

        view.Find(".kh-doc-toc-toggle").Click();

        Assert.False(uiState.OutlineCollapsed);
        Assert.NotEmpty(view.FindAll(".kh-doc-toc-link"));
    }
}
