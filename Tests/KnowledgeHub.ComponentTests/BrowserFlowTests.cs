using Bunit;
using MgSoftDev.KnowledgeHub.Blazor.Components.Embedded;
using Xunit;

namespace KnowledgeHub.ComponentTests;

/// <summary>
/// Internal navigation of <see cref="KnowledgeHubBrowser"/>: the embedded mode never changes the
/// URL, so which screen you end up on is decided entirely in memory — and that is precisely what
/// broke in v0.19.3 with no error anywhere.
/// </summary>
public class BrowserFlowTests : ComponentTestBase
{
    /// <summary>
    /// Clicking a page in the tree opens the reader. Baseline for everything below: if this breaks,
    /// the other assertions mean nothing.
    /// </summary>
    [Fact]
    public void Clic_en_el_arbol_abre_el_lector()
    {
        var browser = Render<KnowledgeHubBrowser>();

        SelectFirstPage(browser);

        Assert.Contains("Contenido de prueba", browser.Markup);
    }

    /// <summary>
    /// THE v0.19.3 REGRESSION. Pressing Edit switched to the editor and then the tree refresh pulled
    /// the view straight back to the reader, silently. Asserting "the editor is showing" is what
    /// catches it; asserting "the click was handled" would have passed even while broken.
    /// </summary>
    [Fact]
    public void Clic_en_Editar_abre_el_editor()
    {
        var browser = Render<KnowledgeHubBrowser>();
        SelectFirstPage(browser);

        browser.FindAll("button").First(b => b.TextContent.Contains("Editar")).Click();

        Assert.True(IsEditorShowing(browser),
            $"Se esperaba el editor y se quedó en el lector. Markup: {Excerpt(browser.Markup)}");
    }

    /// <summary>
    /// The root cause, pinned on its own: reloading the tree re-applies the highlight and Radzen
    /// raises its Change event for it, exactly as if the user had clicked. That echo must not drag
    /// the view anywhere — and it is not hypothetical: saving an icon in Manage also refreshes the
    /// tree, so without the guard that screen threw you out too.
    /// </summary>
    [Fact]
    public async Task Recargar_el_arbol_no_cambia_la_vista()
    {
        var browser = Render<KnowledgeHubBrowser>();
        SelectFirstPage(browser);
        browser.FindAll("button").First(b => b.TextContent.Contains("Editar")).Click();
        Assert.True(IsEditorShowing(browser), "El editor no llegó a abrirse; el test no prueba nada.");

        var loadsBefore = Pages.TreeLoads;
        // InvokeAsync porque tocar el componente desde el hilo del test no pasa por el Dispatcher
        // de Blazor: es la misma gotcha 11 del repo, y aquí salta como excepción del test.
        await browser.InvokeAsync(() => browser.Instance.RefreshTreeAsync());

        Assert.True(Pages.TreeLoads > loadsBefore, "El árbol no se recargó: el test no prueba nada.");
        Assert.True(IsEditorShowing(browser),
            $"Recargar el árbol sacó al usuario del editor. Markup: {Excerpt(browser.Markup)}");
    }

    /// <summary>Volver desde Gestionar devuelve al lector, sin pasar por la pantalla vacía.</summary>
    [Fact]
    public void Volver_desde_Gestionar_devuelve_al_lector()
    {
        var browser = Render<KnowledgeHubBrowser>();
        SelectFirstPage(browser);

        browser.FindAll("button").First(b => b.TextContent.Contains("Gestionar")).Click();
        Assert.Contains("Gestionar página", browser.Markup);

        browser.FindAll("button").First(b => b.TextContent.Contains("Volver")).Click();

        Assert.Contains("Contenido de prueba", browser.Markup);
    }

    // ---------------------------------------------------------------- helpers

    private static void SelectFirstPage(IRenderedComponent<KnowledgeHubBrowser> browser) =>
        browser.FindAll(".kh-tree-label").First().Click();

    /// <summary>
    /// The editor is the only screen carrying the change-note field, so its label is a sturdier
    /// marker than anything inside the Radzen html editor, which needs JS that does not run here.
    /// </summary>
    private static bool IsEditorShowing(IRenderedComponent<KnowledgeHubBrowser> browser) =>
        browser.Markup.Contains("kh-editor-fill") || browser.Markup.Contains("Nota de cambio");

    private static string Excerpt(string markup) =>
        markup.Length <= 400 ? markup : markup[..400] + "…";
}
