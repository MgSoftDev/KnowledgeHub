using MgSoftDev.KnowledgeHub.Dtos;

namespace MgSoftDev.KnowledgeHub.Contracts;

/// <summary>
/// One source of live data the HOST exposes to page templates: a table of equipment, a call to an
/// internal API, a file on disk. Register as many as you need — each one contributes a root
/// variable named after <see cref="Name"/>, e.g. <c>{{ equipos.lineas }}</c>.
///
/// Register them WHERE THE CORE RUNS. In a WASM setup that is the API server, not the browser: the
/// client is a proxy and the page is rendered before it is serialised.
///
/// <para>
/// <b>Do not return rich objects.</b> A template can read every public property of whatever you
/// hand over, recursively. Returning an EF entity drags its navigation properties along, and
/// returning something like a DbContext or an HttpContext exposes far more than you meant. Project
/// to a small anonymous type or a DTO with exactly the fields the documentation needs.
/// </para>
///
/// <para>
/// It is called on EVERY view of a page that uses it, on purpose: the whole point is that the data
/// is current. If your source is expensive, cache inside your implementation — you are the only one
/// who knows what it costs and how stale it may safely get.
/// </para>
/// </summary>
public interface IKnowledgeHubTemplateModelProvider
{
    /// <summary>
    /// Root variable name, as written in the page. Use a plain lowercase identifier
    /// (<c>equipos</c>, <c>lineas</c>); it must not collide with the built-in <c>kh</c>.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// What this model offers, for the editor's model explorer. Purely descriptive — nothing is
    /// enforced against it — so keep it honest or it misleads more than it helps.
    /// </summary>
    TemplateModelInfoDto Describe();

    /// <summary>
    /// The data itself. Returning null leaves the variable empty rather than failing the page.
    /// An exception is caught and reported as a broken block; it never takes the page down.
    /// </summary>
    Task<object?> GetModelAsync(TemplateModelContext context);
}
