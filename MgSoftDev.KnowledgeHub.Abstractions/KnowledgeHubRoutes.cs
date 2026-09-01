namespace MgSoftDev.KnowledgeHub;

/// <summary>
/// Central route table of the KnowledgeHub UI. The RCL pages declare their @page templates
/// to match these constants (route templates are compile-time literals, so keep both sides
/// in sync by hand). The fixed <c>/kh</c> prefix avoids collisions when the module is
/// embedded in a host application.
/// </summary>
public static class KnowledgeHubRoutes
{
    public const string Prefix = "/kh";

    public const string Home = Prefix;
    public const string Diagnostics = $"{Prefix}/diagnostics";

    public static string Page(Guid pagePk) => $"{Prefix}/page/{pagePk}";
    public static string Edit(Guid pagePk) => $"{Prefix}/edit/{pagePk}";
    public static string History(Guid pagePk) => $"{Prefix}/history/{pagePk}";
    public static string Version(Guid versionPk) => $"{Prefix}/version/{versionPk}";
    public static string Permissions(Guid pagePk) => $"{Prefix}/permissions/{pagePk}";
    public static string Manage(Guid pagePk) => $"{Prefix}/manage/{pagePk}";
    public static string Search(string term) => $"{Prefix}/search?q={Uri.EscapeDataString(term)}";

    /// <summary>
    /// True when a path points at a KnowledgeHub page, with the pk it names. Used to recognise the
    /// links an author pastes between pages, so the reader can follow them with its own navigation
    /// instead of letting the browser leave: in embedded mode the module lives inside a host screen
    /// and a plain href would take the user out of it.
    /// <para>
    /// Matches anywhere in the path, not only at the start, because a host may serve the app under
    /// a base path (<c>/miapp/kh/page/{pk}</c>). The leading slash of the segment is what keeps
    /// <c>/notkh/page/…</c> from matching. Anything after the pk —a query, a fragment— is ignored.
    /// Same-origin is NOT checked here: the path is all this sees, so whoever hands it one is
    /// responsible for not handing over another site's.
    /// </para>
    /// </summary>
    public static bool TryGetPagePk(string? path, out Guid pagePk)
    {
        pagePk = Guid.Empty;
        if (string.IsNullOrWhiteSpace(path)) return false;

        const string segment = $"{Prefix}/page/";
        var start = path.IndexOf(segment, StringComparison.OrdinalIgnoreCase);
        if (start < 0) return false;

        var rest = path[(start + segment.Length)..];
        var end = rest.IndexOfAny(['/', '?', '#']);
        if (end >= 0) rest = rest[..end];

        return Guid.TryParse(rest, out pagePk);
    }
}
