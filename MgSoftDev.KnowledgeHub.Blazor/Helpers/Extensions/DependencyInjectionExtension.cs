using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Radzen;

namespace MgSoftDev.KnowledgeHub.Blazor;

public static class DependencyInjectionExtension
{
    /// <summary>
    /// Registers the KnowledgeHub UI: Radzen services + the UI options (editor tools, title).
    /// Host requirements (documented): reference the Radzen theme CSS and
    /// <c>_content/MgSoftDev.KnowledgeHub.Blazor/knowledgehub.css</c> in the host page, place
    /// <c>&lt;RadzenComponents /&gt;</c> in the root component, and add this assembly to the
    /// router via <see cref="KnowledgeHubAssemblyMarker"/>.
    /// </summary>
    public static IServiceCollection AddKnowledgeHubBlazor(this IServiceCollection services,
        Action<KnowledgeHubBlazorOptions>? configure = null)
    {
        var options = new KnowledgeHubBlazorOptions();
        configure?.Invoke(options);
        services.AddSingleton(options);

        // Fallback UI options for hosts that do not register the core (e.g. WASM clients).
        services.TryAddSingleton(new KnowledgeHubOptions());

        services.AddRadzenComponents();
        return services;
    }
}
