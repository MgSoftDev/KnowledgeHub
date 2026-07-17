using MgSoftDev.KnowledgeHub.Contracts;
using MgSoftDev.KnowledgeHub.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace MgSoftDev.KnowledgeHub.Http.Client;

public static class DependencyInjectionExtension
{
    /// <summary>
    /// Registers the HTTP implementations of the KnowledgeHub service contracts. The host must
    /// have registered an <see cref="HttpClient"/> (scoped or singleton) whose BaseAddress
    /// points at the server that maps MapKnowledgeHubApi — in WASM that is the standard
    /// <c>builder.Services.AddScoped(sp =&gt; new HttpClient { BaseAddress = ... })</c>.
    /// The host still registers its own IKnowledgeHubUserContext (e.g. populated from /kh/api/me).
    /// </summary>
    public static IServiceCollection AddKnowledgeHubHttpClient(this IServiceCollection services,
        Action<KnowledgeHubHttpClientOptions>? configure = null)
    {
        var options = new KnowledgeHubHttpClientOptions();
        configure?.Invoke(options);
        services.AddSingleton(options);

        services.AddScoped<KnowledgeHubApiClient>();
        services.AddScoped<IKnowledgeHubPageService, HttpKnowledgeHubPageService>();
        services.AddScoped<IKnowledgeHubImageService, HttpKnowledgeHubImageService>();
        services.AddScoped<IKnowledgeHubHtmlImageRewriter, HttpKnowledgeHubHtmlImageRewriter>();
        services.TryAddScoped<IKnowledgeHubDiagnostics, InMemoryKnowledgeHubDiagnostics>();

        return services;
    }
}
