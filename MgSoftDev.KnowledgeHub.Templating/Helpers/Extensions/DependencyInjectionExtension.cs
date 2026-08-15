using MgSoftDev.KnowledgeHub.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace MgSoftDev.KnowledgeHub.Templating;

public static class DependencyInjectionExtension
{
    /// <summary>
    /// Registers the default template engine. Once registered, pages marked as dynamic get their
    /// <c>{{ … }}</c> placeholders filled when displayed. Without it those pages simply show their
    /// placeholders as written — nothing breaks, nothing is lost.
    ///
    /// Register it WHERE THE CORE RUNS: the same container as <c>AddKnowledgeHubCore</c>. In a WASM
    /// setup that is the API server, not the browser — the client is a proxy and the page is
    /// rendered before it is serialised, so Scriban never reaches the browser.
    ///
    /// <para>
    /// Two things are worth registering alongside it. A <see cref="IKnowledgeHubHtmlSanitizer"/>,
    /// because a template pastes host data into the page and Scriban does not escape anything; and
    /// your own <see cref="IKnowledgeHubTemplateModelProvider"/> implementations, which are what
    /// gives pages something to show beyond the built-in <c>kh</c>.
    /// </para>
    /// </summary>
    /// <param name="services">The host's service collection.</param>
    /// <param name="configure">Execution limits. The defaults suit documentation; raise them knowingly.</param>
    public static IServiceCollection AddKnowledgeHubTemplating(this IServiceCollection services,
        Action<KnowledgeHubTemplateOptions>? configure = null)
    {
        var options = new KnowledgeHubTemplateOptions();
        configure?.Invoke(options);

        // TryAdd: a host that registered its own IKnowledgeHubTemplateRenderer beforehand wins.
        services.TryAddSingleton<IKnowledgeHubTemplateRenderer>(new ScribanTemplateRenderer(options));
        return services;
    }
}
