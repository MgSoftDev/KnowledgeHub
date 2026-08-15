namespace MgSoftDev.KnowledgeHub.Templating;

/// <summary>
/// Limits on what a page template may do. They exist because a template is written by an editor but
/// executed by the SERVER, on every view, for every reader: an accidental runaway loop is an outage,
/// not a broken page.
///
/// The defaults are deliberately tighter than Scriban's own (1000 iterations, 100 levels, 1 MB).
/// Documentation lists things; it does not compute.
/// </summary>
public sealed class KnowledgeHubTemplateOptions
{
    /// <summary>
    /// Wall clock a single page render may take. Scriban checks for cancellation before EVERY
    /// statement, so this bites even in a tight loop — but it cannot interrupt a single slow call
    /// inside a host's data provider, which is why providers get the token too.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 2;

    /// <summary>
    /// Total iterations allowed per top-level loop nest. Careful, it is a BUDGET, not a per-loop
    /// cap: two nested loops of 100 share it, so they stop at 500 rather than reaching 10.000.
    /// </summary>
    public int LoopLimit { get; set; } = 500;

    /// <summary>How deep function calls may nest.</summary>
    public int RecursiveLimit { get; set; } = 20;

    /// <summary>
    /// Ceiling on the output of one render. Past it Scriban stops writing and appends "…" — it does
    /// NOT throw, so an oversized page comes back truncated rather than failing.
    /// </summary>
    public int MaxOutputChars { get; set; } = 256 * 1024;
}
