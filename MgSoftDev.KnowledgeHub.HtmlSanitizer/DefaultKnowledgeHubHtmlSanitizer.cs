using MgSoftDev.KnowledgeHub.Contracts;

namespace MgSoftDev.KnowledgeHub.HtmlSanitizer;

/// <summary>
/// <see cref="IKnowledgeHubHtmlSanitizer"/> backed by the HtmlSanitizer (Ganss.Xss) library.
/// Holds one configured instance per <see cref="HtmlCleanupLevel"/>; the level only reaches here
/// from the paste path and the manual cleanup button, while saving always uses Standard.
///
/// Safe as a singleton: the underlying sanitizer produces identical output under concurrent use,
/// and every level is idempotent (sanitizing twice equals sanitizing once), which the save path
/// and the "nothing to clean" message rely on.
/// </summary>
public sealed class DefaultKnowledgeHubHtmlSanitizer : IKnowledgeHubHtmlSanitizer
{
    private readonly Ganss.Xss.HtmlSanitizer _standard;
    private readonly Ganss.Xss.HtmlSanitizer _strict;
    private readonly Ganss.Xss.HtmlSanitizer _plainText;

    /// <param name="standard">
    /// The permissive instance, already customised by the host through
    /// <c>AddKnowledgeHubHtmlSanitizer(configure)</c>. The two stricter levels are derived from
    /// the defaults, so a host that widens Standard does not accidentally widen them too.
    /// </param>
    public DefaultKnowledgeHubHtmlSanitizer(Ganss.Xss.HtmlSanitizer standard)
    {
        _standard = standard;
        _strict = KnowledgeHubSanitizerDefaults.CreateSanitizer(HtmlCleanupLevel.Strict);
        _plainText = KnowledgeHubSanitizerDefaults.CreateSanitizer(HtmlCleanupLevel.PlainText);
    }

    /// <summary>
    /// Builds the three levels from the same options, which is the only way the host reaches levels
    /// 2 and 3 — the constructor above can only ever customise level 1, so a layout declared there
    /// still vanished the moment someone pressed the cleanup button at level 2.
    /// </summary>
    public DefaultKnowledgeHubHtmlSanitizer(KnowledgeHubSanitizerOptions options)
    {
        _standard = KnowledgeHubSanitizerDefaults.CreateSanitizer(HtmlCleanupLevel.Standard, options);
        _strict = KnowledgeHubSanitizerDefaults.CreateSanitizer(HtmlCleanupLevel.Strict, options);
        _plainText = KnowledgeHubSanitizerDefaults.CreateSanitizer(HtmlCleanupLevel.PlainText, options);
    }

    /// <summary>Creates one with the KnowledgeHub defaults.</summary>
    public DefaultKnowledgeHubHtmlSanitizer() : this(KnowledgeHubSanitizerDefaults.CreateSanitizer()) { }

    /// <inheritdoc />
    public string Sanitize(string html, HtmlSanitizeContext context) =>
        Sanitize(html, context, HtmlCleanupLevel.Standard);

    /// <inheritdoc />
    public string Sanitize(string html, HtmlSanitizeContext context, HtmlCleanupLevel level)
    {
        if (string.IsNullOrEmpty(html)) return html;

        // The pre-process is what keeps script/style text out and stops paragraphs from being
        // glued together; the sanitizer's own hooks run too late to do either.
        var prepared = HtmlPreProcessor.Prepare(html, level);

        return level switch
        {
            HtmlCleanupLevel.Strict => _strict.Sanitize(prepared),
            HtmlCleanupLevel.PlainText => _plainText.Sanitize(prepared),
            _ => _standard.Sanitize(prepared)
        };
    }
}
