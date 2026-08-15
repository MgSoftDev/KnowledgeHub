using Bunit;
using MgSoftDev.KnowledgeHub;
using MgSoftDev.KnowledgeHub.Blazor;
using MgSoftDev.KnowledgeHub.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Radzen;

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
}
