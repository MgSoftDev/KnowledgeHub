using Bunit;
using MgSoftDev.KnowledgeHub;
using MgSoftDev.KnowledgeHub.Blazor;
using MgSoftDev.KnowledgeHub.Contracts;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Radzen;
using Radzen.Blazor;

namespace KnowledgeHub.ComponentTests;

/// <summary>
/// Everything the KnowledgeHub components need to render outside a browser.
///
/// bUnit renders Blazor in memory, so these tests catch what compiling cannot: a component that
/// blows up on render, or a flow that ends on the wrong screen. What they do NOT catch is a core
/// service returning the wrong data — the services here are fakes, and checking them is the parity
/// harness's job.
/// </summary>
public abstract class ComponentTestBase : BunitContext
{
    protected FakePageService Pages { get; } = new();

    protected ComponentTestBase()
    {
        // Radzen and knowledgehub.js call into JS constantly (localStorage, measuring, the editor).
        // Loose mode answers every call with a default instead of throwing, which is what lets a
        // component render at all here.
        JSInterop.Mode = JSRuntimeMode.Loose;

        Services.AddRadzenComponents();
        Services.AddSingleton(new KnowledgeHubBlazorOptions());
        Services.AddSingleton(new KnowledgeHubOptions());
        Services.AddScoped<KnowledgeHubUiState>();
        Services.AddScoped<IKnowledgeHubUserContext>(_ => new FakeUser());
        Services.AddScoped<IKnowledgeHubPageService>(_ => Pages);
        Services.AddScoped<IKnowledgeHubHtmlImageRewriter>(_ => new FakeImageRewriter());
        Services.AddScoped<IKnowledgeHubDiagnostics, FakeDiagnostics>();
        Services.AddScoped<IKnowledgeHubImageService, FakeImageService>();
    }

    /// <summary>
    /// Mounts Radzen's overlay hosts, the way a host app puts <c>&lt;RadzenComponents /&gt;</c>
    /// beside its Router. Render it before the component under test and read the menus and dialogs
    /// off ITS markup — they are painted there, not inside the component that asked for them.
    ///
    /// It matters for anything going through ContextMenuService, DialogService or
    /// NotificationService: without the host mounted those services raise an event nobody is
    /// listening to and return normally, so a menu that never opens would look perfectly green.
    /// </summary>
    protected IRenderedComponent<RadzenComponents> RenderRadzenOverlays() => Render<RadzenComponents>();
}
