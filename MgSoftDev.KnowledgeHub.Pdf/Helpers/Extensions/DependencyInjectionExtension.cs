using MgSoftDev.KnowledgeHub.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace MgSoftDev.KnowledgeHub.Pdf;

public static class DependencyInjectionExtension
{
    /// <summary>
    /// Registers the default PDF renderer. Once registered, the export button shows up for users
    /// who may export, and the export endpoint starts producing files. Without it the library
    /// behaves exactly as before — nothing to export, no button.
    ///
    /// Register it wherever the CORE runs: the same container as AddKnowledgeHubCore. In a WASM
    /// setup that means the API server, not the client.
    /// </summary>
    /// <param name="services">The host's service collection.</param>
    /// <param name="configure">Where the browser comes from, the theme, and the cover/header/footer.</param>
    public static IServiceCollection AddKnowledgeHubPdf(this IServiceCollection services,
        Action<KnowledgeHubPdfOptions>? configure = null)
    {
        var options = new KnowledgeHubPdfOptions();
        configure?.Invoke(options);

        // TryAdd: a host that registered its own IKnowledgeHubPdfRenderer beforehand wins.
        services.TryAddSingleton<IKnowledgeHubPdfRenderer>(new PlaywrightPdfRenderer(options));
        return services;
    }
}
