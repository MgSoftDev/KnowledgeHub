using MgSoftDev.KnowledgeHub.Dtos;
using MgSoftDev.ReturningCore;

namespace MgSoftDev.KnowledgeHub.Contracts;

/// <summary>
/// Fills a page's placeholders with live data when it is displayed, so documentation that lists
/// things which change — the system's roles, the plant's PCs and PLCs — stops being a copy that
/// somebody has to remember to update.
///
/// Optional, like the sanitizer and the PDF renderer: the library resolves it with
/// <c>GetService&lt;T&gt;()</c>, and without one registered a page marked as dynamic simply shows its
/// placeholders as written. Install <c>MgSoftDev.KnowledgeHub.Templating</c> for the default engine
/// (Scriban), or implement this over whatever your organisation already uses.
///
/// It renders ONLY pages the author marked as dynamic and only if that author holds
/// <see cref="Security.KnowledgeHubPermissions.Templates"/>. Everything else is left untouched, so
/// a page documenting Angular or Handlebars keeps its braces exactly as typed.
///
/// Implementations must be safe to use as a singleton, and must treat the template as UNTRUSTED
/// input: it is written by editors, but it runs on the server, on every page view.
/// </summary>
public interface IKnowledgeHubTemplateRenderer
{
    /// <summary>
    /// Checks the syntax WITHOUT executing anything. An empty list means the template is fine.
    /// Used before saving (to warn) and before publishing (to refuse), so a broken template never
    /// reaches a reader.
    /// </summary>
    ReturningList<TemplateErrorDto> Validate(string html);

    /// <summary>
    /// Renders the page. A failure here must NEVER be fatal: the caller shows the page with the
    /// broken block replaced by a notice, because losing a whole manual over one bad expression is
    /// worse than showing it with a hole.
    /// </summary>
    Task<Returning<string>> RenderAsync(string html, TemplateRenderContext context);
}
