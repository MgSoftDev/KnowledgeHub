namespace MgSoftDev.KnowledgeHub.Store;

/// <summary>
/// Visibility rule a store MUST apply inside its queries (never after materialization):
/// a page is visible when it is public OR any of its active DocPagePermissions matches one
/// of <see cref="Permissions"/> (case-insensitive). <see cref="SeesEverything"/> bypasses
/// the filter entirely (admin).
/// </summary>
public sealed record VisibilityFilter(bool SeesEverything, IReadOnlyList<string> Permissions)
{
    public static VisibilityFilter Admin { get; } = new(true, Array.Empty<string>());
}
