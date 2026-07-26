using MgSoftDev.KnowledgeHub.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace MgSoftDev.KnowledgeHub.HtmlSanitizer;

public static class DependencyInjectionExtension
{
    /// <summary>
    /// Registers the default HTML sanitizer. Once registered, KnowledgeHub cleans pasted content,
    /// the manual cleanup button works, and every version is checked right before it is stored.
    /// Without it the library keeps working exactly as before — nothing is cleaned.
    ///
    /// Register it in EVERY container that needs it: the UI container cleans on paste, the
    /// container running the core cleans on save. In a WASM setup that means both the client and
    /// the API server.
    /// </summary>
    /// <param name="services">The host's service collection.</param>
    /// <param name="configure">
    /// Widen or tighten the rules, e.g. <c>o => o.AllowedTags.Add("iframe")</c>. Starts from
    /// <see cref="KnowledgeHubSanitizerDefaults.CreateSanitizer"/>, so the KnowledgeHub-specific
    /// schemes and CSS properties are already in place.
    /// </param>
    public static IServiceCollection AddKnowledgeHubHtmlSanitizer(this IServiceCollection services,
        Action<Ganss.Xss.HtmlSanitizer>? configure = null)
    {
        var sanitizer = KnowledgeHubSanitizerDefaults.CreateSanitizer();
        configure?.Invoke(sanitizer);

        // TryAdd: a host that registered its own IKnowledgeHubHtmlSanitizer beforehand wins.
        services.TryAddSingleton<IKnowledgeHubHtmlSanitizer>(new DefaultKnowledgeHubHtmlSanitizer(sanitizer));
        return services;
    }
}
