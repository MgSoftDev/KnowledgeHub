using Bunit;
using MgSoftDev.KnowledgeHub.Blazor.Components.Embedded;
using Microsoft.AspNetCore.Components;
using Xunit;

namespace KnowledgeHub.ComponentTests;

/// <summary>
/// Every embeddable component, mounted once. Deliberately shallow: it only asks whether the thing
/// renders at all.
///
/// That is not a low bar — it is the exact bar that would have caught the worst bug of the lot. A
/// Razor comment written inside a component's attribute list compiles without a murmur, and Razor
/// treats it as an attribute name, so the failure only shows up when the component is RENDERED.
/// It shipped in three releases and broke the entire embedded mode.
/// </summary>
public class SmokeTests : ComponentTestBase
{
    public static TheoryData<string> Components =>
    [
        nameof(KnowledgeHubBrowser), nameof(KnowledgeHubNavTree), nameof(KnowledgeHubPageView),
        nameof(KnowledgeHubPageEditor), nameof(KnowledgeHubPageHistory), nameof(KnowledgeHubVersionView),
        nameof(KnowledgeHubPagePermissions), nameof(KnowledgeHubPageManage),
        nameof(KnowledgeHubSearchResults), nameof(KnowledgeHubDiagnosticsPanel),
        nameof(KnowledgeHubSplitLayout), nameof(KnowledgeHubIconPicker), nameof(KnowledgeHubPageIcon)
    ];

    [Theory]
    [MemberData(nameof(Components))]
    public void Monta_sin_reventar(string component)
    {
        var markup = Render(component).Markup;

        Assert.False(string.IsNullOrWhiteSpace(markup),
            $"{component} se montó pero no pintó nada.");
    }

    /// <summary>
    /// Mounts one component with the minimum parameters it needs. Kept as a switch rather than
    /// reflection so a component added later fails to compile here until someone decides how it
    /// should be mounted — a silently skipped component is worse than no test.
    /// </summary>
    private IRenderedComponent<IComponent> Mount(string component) => component switch
    {
        nameof(KnowledgeHubBrowser) => Render<KnowledgeHubBrowser>(),
        nameof(KnowledgeHubNavTree) => Render<KnowledgeHubNavTree>(),

        nameof(KnowledgeHubPageView) => Render<KnowledgeHubPageView>(
            p => p.Add(c => c.PagePk, FakePageService.RootPk)),
        // El editor entra con una salvedad: RadzenHtmlEditor es JS puro, así que esto comprueba que
        // el componente monta, NO que se pueda escribir en él.
        nameof(KnowledgeHubPageEditor) => Render<KnowledgeHubPageEditor>(
            p => p.Add(c => c.PagePk, FakePageService.RootPk)),
        nameof(KnowledgeHubPageHistory) => Render<KnowledgeHubPageHistory>(
            p => p.Add(c => c.PagePk, FakePageService.RootPk)),
        nameof(KnowledgeHubVersionView) => Render<KnowledgeHubVersionView>(
            p => p.Add(c => c.VersionPk, FakePageService.VersionPk)),
        nameof(KnowledgeHubPagePermissions) => Render<KnowledgeHubPagePermissions>(
            p => p.Add(c => c.PagePk, FakePageService.RootPk)),
        nameof(KnowledgeHubPageManage) => Render<KnowledgeHubPageManage>(
            p => p.Add(c => c.PagePk, FakePageService.RootPk)),
        nameof(KnowledgeHubSearchResults) => Render<KnowledgeHubSearchResults>(
            p => p.Add(c => c.Term, "manual")),
        nameof(KnowledgeHubDiagnosticsPanel) => Render<KnowledgeHubDiagnosticsPanel>(),
        nameof(KnowledgeHubSplitLayout) => Render<KnowledgeHubSplitLayout>(
            p => p.Add(c => c.MainContent, (RenderFragment)(b => b.AddMarkupContent(0, "<p>x</p>")))),
        nameof(KnowledgeHubIconPicker) => Render<KnowledgeHubIconPicker>(),
        nameof(KnowledgeHubPageIcon) => Render<KnowledgeHubPageIcon>(
            p => p.Add(c => c.Icon, "article")),

        _ => throw new ArgumentOutOfRangeException(nameof(component), component,
            "Componente sin montaje definido: añádelo aquí.")
    };

    private IRenderedComponent<IComponent> Render(string component) => Mount(component);
}
