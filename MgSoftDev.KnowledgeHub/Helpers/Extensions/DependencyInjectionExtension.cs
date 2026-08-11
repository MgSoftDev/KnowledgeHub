using MgSoftDev.KnowledgeHub.Contracts;
using MgSoftDev.KnowledgeHub.Diagnostics;
using MgSoftDev.KnowledgeHub.Imaging;
using MgSoftDev.KnowledgeHub.Seeding;
using MgSoftDev.KnowledgeHub.Services;
using Microsoft.Extensions.DependencyInjection;

namespace MgSoftDev.KnowledgeHub;

public static class DependencyInjectionExtension
{
    /// <summary>
    /// Registers the KnowledgeHub core services. Everything is Scoped so the same registration
    /// works across hosting models (WPF Hybrid: the root scope lives app-long; Blazor Server:
    /// one scope per circuit/user; WASM: scope ≈ singleton).
    ///
    /// The host must additionally register:
    /// <list type="bullet">
    /// <item>a storage provider (AddKnowledgeHubLiteDbStore / AddKnowledgeHubSqlServerStore /
    /// a custom <see cref="IKnowledgeHubStore"/>),</item>
    /// <item>its <see cref="IKnowledgeHubUserContext"/> implementation, and</item>
    /// <item>optionally <see cref="AddKnowledgeHubFileImageCache"/> when local disk is available.</item>
    /// </list>
    /// </summary>
    public static IServiceCollection AddKnowledgeHubCore(this IServiceCollection services,
        Action<KnowledgeHubOptions>? configure = null)
    {
        var options = new KnowledgeHubOptions();
        configure?.Invoke(options);
        services.AddSingleton(options);

        services.AddSingleton<IKnowledgeHubImageUrlResolver>(new DefaultImageUrlResolver(options.PublicAssetsBaseUrl));
        // Explicit factory (not reflection activation) because the sanitizer is OPTIONAL: it only
        // exists when the host registers one. Same pattern as the image cache below.
        services.AddScoped<IKnowledgeHubPageService>(sp => new KnowledgeHubPageService(
            sp.GetRequiredService<IKnowledgeHubStore>(),
            sp.GetRequiredService<IKnowledgeHubUserContext>(),
            sp.GetRequiredService<IKnowledgeHubImageService>(),
            sp.GetRequiredService<KnowledgeHubOptions>(),
            sp.GetService<IKnowledgeHubHtmlSanitizer>()));
        services.AddScoped<IKnowledgeHubImageService, KnowledgeHubImageService>();
        services.AddScoped<IKnowledgeHubHtmlImageRewriter>(sp => new KnowledgeHubHtmlImageRewriter(
            sp.GetRequiredService<IKnowledgeHubStore>(),
            sp.GetRequiredService<IKnowledgeHubImageUrlResolver>(),
            sp.GetService<IKnowledgeHubImageCache>()));
        services.AddScoped<IKnowledgeHubDiagnostics, InMemoryKnowledgeHubDiagnostics>();
        services.AddScoped<KnowledgeHubContentSeeder>();

        // The PDF renderer is OPTIONAL too (MgSoftDev.KnowledgeHub.Pdf, or the host's own), so the
        // export service is built by hand as well. Registered unconditionally: without a renderer
        // it still builds the document and reports politely that nothing can render it.
        services.AddScoped<IKnowledgeHubPdfExportService>(sp => new KnowledgeHubPdfExportService(
            sp.GetRequiredService<IKnowledgeHubPageService>(),
            sp.GetRequiredService<IKnowledgeHubStore>(),
            sp.GetRequiredService<IKnowledgeHubUserContext>(),
            sp.GetRequiredService<KnowledgeHubOptions>(),
            sp.GetService<IKnowledgeHubPdfRenderer>()));

        return services;
    }

    /// <summary>
    /// Registers the optional hash-addressed disk cache (hosts with a local disk: WPF, Blazor
    /// Server). Default folder: %LOCALAPPDATA%\KnowledgeHub\cache.
    /// </summary>
    public static IServiceCollection AddKnowledgeHubFileImageCache(this IServiceCollection services,
        string? cacheFolder = null)
    {
        services.AddScoped<IKnowledgeHubImageCache>(sp =>
            new FileSystemImageCache(sp.GetRequiredService<IKnowledgeHubStore>(), cacheFolder));
        return services;
    }
}
