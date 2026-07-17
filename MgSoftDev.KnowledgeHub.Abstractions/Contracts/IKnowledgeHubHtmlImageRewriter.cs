using MgSoftDev.KnowledgeHub.Dtos;
using MgSoftDev.ReturningCore;

namespace MgSoftDev.KnowledgeHub.Contracts;

/// <summary>
/// Turns stored HTML (with stable <c>docimg://{pk}</c> references) into displayable HTML.
/// This is the contract UI components inject; each hosting model registers a fitting
/// implementation (file cache + virtual host in WPF, HTTP asset endpoint in Blazor Server,
/// remote API call in WASM).
/// </summary>
public interface IKnowledgeHubHtmlImageRewriter
{
    /// <summary>
    /// Rewrites every <c>docimg://{pk}</c> in <paramref name="storedHtml"/> to a display URL
    /// (always ending in <c>{contentHash}.webp</c>), ensuring binaries are locally cached
    /// first when a cache is available. Returns the HTML plus per-navigation metrics.
    /// </summary>
    Task<Returning<HtmlRewriteResult>> PrepareForDisplayAsync(string storedHtml);
}
