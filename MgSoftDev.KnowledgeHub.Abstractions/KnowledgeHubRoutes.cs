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
}
