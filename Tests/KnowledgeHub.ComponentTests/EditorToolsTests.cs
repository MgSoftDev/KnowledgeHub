using Bunit;
using MgSoftDev.KnowledgeHub.Blazor.Components.Embedded;
using MgSoftDev.KnowledgeHub.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace KnowledgeHub.ComponentTests;

/// <summary>
/// The editor toolbar: which custom buttons appear, and in which view they can be pressed.
///
/// The second half matters more than it looks. Radzen's custom tools default to being enabled in
/// DESIGN view only, and this library never set that parameter — so every one of its own buttons
/// was quietly greyed out while looking at the code. A button that cannot be pressed and says
/// nothing is indistinguishable from a broken one.
///
/// Out of reach here: what the formatter produces (that is the parity harness's job, 21 checks) and
/// reading the code view through JS, which bUnit does not run.
/// </summary>
public class EditorToolsTests : ComponentTestBase
{
    private const string FormatTitle = "Formatear el HTML para poder leerlo";
    private const string BroomTitle = "Aplicar la limpieza a todo el documento";

    /// <summary>Sin nada registrado que formatee, el botón no tiene qué hacer y no se pinta.</summary>
    [Fact]
    public void Sin_formateador_no_hay_boton_de_formatear()
    {
        var editor = Render<KnowledgeHubPageEditor>(p => p.Add(e => e.PagePk, FakePageService.RootPk));

        Assert.Empty(FindButtons(editor, FormatTitle));
    }

    [Fact]
    public void Con_formateador_registrado_aparece()
    {
        Services.AddSingleton<IKnowledgeHubHtmlFormatter>(new FakeHtmlFormatter());

        var editor = Render<KnowledgeHubPageEditor>(p => p.Add(e => e.PagePk, FakePageService.RootPk));

        Assert.Single(FindButtons(editor, FormatTitle));
    }

    /// <summary>
    /// El editor arranca en vista diseño, así que el de formatear nace deshabilitado y la escoba no.
    /// Es la prueba de que EnabledModes llega hasta el botón de Radzen: sin pasarlo, los dos
    /// saldrían habilitados en diseño y apagados en código, que es justo lo contrario de lo que
    /// hace falta.
    /// </summary>
    [Fact]
    public void En_vista_diseno_el_de_formatear_nace_apagado_y_la_escoba_no()
    {
        Services.AddSingleton<IKnowledgeHubHtmlFormatter>(new FakeHtmlFormatter());
        Services.AddSingleton<IKnowledgeHubHtmlSanitizer>(new FakeSanitizer());

        var editor = Render<KnowledgeHubPageEditor>(p => p.Add(e => e.PagePk, FakePageService.RootPk));

        Assert.Contains("rz-state-disabled", FindButtons(editor, FormatTitle).Single().ClassName);
        Assert.DoesNotContain("rz-state-disabled", FindButtons(editor, BroomTitle).Single().ClassName);
    }

    private static IEnumerable<AngleSharp.Dom.IElement> FindButtons(
        IRenderedComponent<KnowledgeHubPageEditor> editor, string title) =>
        editor.FindAll("button").Where(b => b.GetAttribute("title") == title);
}

/// <summary>Sanitizer that changes nothing: enough for the broom button to be rendered.</summary>
public sealed class FakeSanitizer : IKnowledgeHubHtmlSanitizer
{
    public string Sanitize(string html, HtmlSanitizeContext context) => html;
}
