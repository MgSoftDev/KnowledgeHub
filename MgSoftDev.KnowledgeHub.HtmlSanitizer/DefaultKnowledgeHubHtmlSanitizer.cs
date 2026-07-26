using MgSoftDev.KnowledgeHub.Contracts;

namespace MgSoftDev.KnowledgeHub.HtmlSanitizer;

/// <summary>
/// <see cref="IKnowledgeHubHtmlSanitizer"/> backed by the HtmlSanitizer (Ganss.Xss) library.
/// Applies the same rules in every context; a host that needs paste to be stricter than save can
/// implement the interface itself and branch on the <see cref="HtmlSanitizeContext"/>.
///
/// Safe as a singleton: the underlying sanitizer was verified to produce identical output under
/// concurrent use, and the pass is idempotent (sanitizing twice equals sanitizing once), which the
/// save path relies on to detect real changes.
/// </summary>
public sealed class DefaultKnowledgeHubHtmlSanitizer : IKnowledgeHubHtmlSanitizer
{
    private readonly Ganss.Xss.HtmlSanitizer _sanitizer;

    /// <param name="sanitizer">Configured instance; see <see cref="KnowledgeHubSanitizerDefaults"/>.</param>
    public DefaultKnowledgeHubHtmlSanitizer(Ganss.Xss.HtmlSanitizer sanitizer) => _sanitizer = sanitizer;

    /// <summary>Creates one with the KnowledgeHub defaults.</summary>
    public DefaultKnowledgeHubHtmlSanitizer() : this(KnowledgeHubSanitizerDefaults.CreateSanitizer()) { }

    /// <inheritdoc />
    public string Sanitize(string html, HtmlSanitizeContext context) =>
        string.IsNullOrEmpty(html) ? html : _sanitizer.Sanitize(html);
}
