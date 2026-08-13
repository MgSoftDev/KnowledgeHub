namespace MgSoftDev.KnowledgeHub.Store;

/// <summary>
/// Visibility rule a store MUST apply inside its queries (never after materialization).
/// A page is visible when:
/// <list type="number">
/// <item>it is public, OR</item>
/// <item>any of its active DocPagePermissions matches one of <see cref="Permissions"/>
/// (case-insensitive), OR</item>
/// <item><see cref="SeesUnconfigured"/> and the page is NOT public and has NO active permission
/// row at all.</item>
/// </list>
/// <see cref="SeesEverything"/> bypasses the filter entirely (admin).
/// </summary>
/// <param name="SeesEverything">Admin: skips the filter altogether.</param>
/// <param name="Permissions">The asking user's permission strings, from the host's catalog.</param>
/// <param name="SeesUnconfigured">
/// True for users who can edit. It exists because a page is created with no permissions and not
/// public, so under rules 1 and 2 alone a brand new page is invisible TO ITS OWN AUTHOR — you could
/// create a page, publish it, and never find it again. An unconfigured page is hidden from everyone,
/// so showing it to whoever may edit reveals nothing that was being protected; it just gives the
/// author a way back to it. The moment someone configures the page, rule 3 stops applying and the
/// normal rules take over.
/// </param>
public sealed record VisibilityFilter(bool SeesEverything, IReadOnlyList<string> Permissions,
    bool SeesUnconfigured = false)
{
    public static VisibilityFilter Admin { get; } = new(true, Array.Empty<string>());
}
