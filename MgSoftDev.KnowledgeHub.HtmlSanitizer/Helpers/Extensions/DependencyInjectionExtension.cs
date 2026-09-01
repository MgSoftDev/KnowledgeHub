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
    /// <see cref="KnowledgeHubSanitizerDefaults.CreateSanitizer()"/>, so the KnowledgeHub-specific
    /// schemes and CSS properties are already in place. Note this configures the STANDARD level
    /// only; the stricter levels are derived from the defaults so widening one does not widen all.
    /// </param>
    public static IServiceCollection AddKnowledgeHubHtmlSanitizer(this IServiceCollection services,
        Action<Ganss.Xss.HtmlSanitizer>? configure = null)
    {
        var sanitizer = KnowledgeHubSanitizerDefaults.CreateSanitizer();
        configure?.Invoke(sanitizer);

        // TryAdd: a host that registered its own IKnowledgeHubHtmlSanitizer beforehand wins.
        // Careful: it also means an EARLIER plain AddKnowledgeHubHtmlSanitizer() call wins over
        // this one, and your configuration is dropped without a word. Register it once.
        services.TryAddSingleton<IKnowledgeHubHtmlSanitizer>(new DefaultKnowledgeHubHtmlSanitizer(sanitizer));
        return services.AddKnowledgeHubHtmlFormatter();
    }

    /// <summary>
    /// Same, configured through <see cref="KnowledgeHubSanitizerOptions"/>, which reaches ALL THREE
    /// cleanup levels — the overload above only ever reaches level 1, so classes declared there
    /// still disappeared when the user pressed the cleanup button at level 2.
    ///
    /// Apply the SAME options in every container that cleans. In a WASM setup the client cleans on
    /// paste and the API server cleans on save, so configuring only the client looks perfect in the
    /// editor and then strips the markup on the way to the database.
    ///
    /// It takes the options INSTANCE rather than a configuration lambda for two reasons: a second
    /// <c>Action&lt;…&gt;</c> overload would make every existing call ambiguous (CS0121), and passing
    /// the same object to both containers is precisely the habit that prevents the two of them from
    /// drifting apart.
    /// </summary>
    public static IServiceCollection AddKnowledgeHubHtmlSanitizer(this IServiceCollection services,
        KnowledgeHubSanitizerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        services.TryAddSingleton<IKnowledgeHubHtmlSanitizer>(new DefaultKnowledgeHubHtmlSanitizer(options));
        return services.AddKnowledgeHubHtmlFormatter();
    }

    /// <summary>
    /// Registers the HTML formatter, which puts the "format the html" button in the editor's code
    /// view. Both <c>AddKnowledgeHubHtmlSanitizer</c> overloads already call this, so a host that
    /// cleans also formats; it is public for the host that wants only the formatter.
    ///
    /// This one belongs in the UI container only — formatting is an authoring convenience, and
    /// nothing on the save path uses it. In a WASM setup that means the client, not the API server.
    /// </summary>
    public static IServiceCollection AddKnowledgeHubHtmlFormatter(this IServiceCollection services)
    {
        services.TryAddSingleton<IKnowledgeHubHtmlFormatter>(new KnowledgeHubHtmlFormatter());
        return services;
    }
}
