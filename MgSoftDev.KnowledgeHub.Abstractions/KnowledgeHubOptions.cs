namespace MgSoftDev.KnowledgeHub;

/// <summary>Cross-cutting KnowledgeHub options, configured by the host at registration time.</summary>
public sealed class KnowledgeHubOptions
{
    /// <summary>
    /// Base URL under which image assets are served, used by the default URL resolver to build
    /// <c>{base}/{hash}.webp</c> display URLs. Typical values: <c>https://docs-assets</c> for a
    /// WPF WebView2 virtual host, <c>/kh/assets</c> for a Blazor Server host, or the absolute
    /// assets endpoint of a remote API for WASM clients.
    /// </summary>
    public string PublicAssetsBaseUrl { get; set; } = "/kh/assets";

    /// <summary>
    /// When false (default, parity mode) anyone holding KnowledgeHub.Edit can publish.
    /// When true, publishing additionally requires KnowledgeHub.Publish (or Admin).
    /// </summary>
    public bool UseFineGrainedPublish { get; set; }

    /// <summary>
    /// When false (default, parity mode) anyone holding KnowledgeHub.Edit can manage page
    /// visibility. When true, it additionally requires KnowledgeHub.ManagePermissions (or Admin).
    /// </summary>
    public bool UseFineGrainedManagePermissions { get; set; }

    /// <summary>Maximum image width in pixels; wider uploads are resized down preserving ratio.</summary>
    public int MaxImageWidth { get; set; } = 1600;

    /// <summary>
    /// When false (default) anyone signed in can export the pages they are allowed to read.
    /// When true, exporting additionally requires KnowledgeHub.Export (or Admin).
    /// </summary>
    public bool UseFineGrainedExport { get; set; }

    /// <summary>
    /// Ceiling on how many pages a single export may contain. Exporting a deep branch loads every
    /// page and every image into memory at once, so an unbounded export is a way to take the
    /// server down. Above the limit the export is REJECTED with a message naming it — it is never
    /// silently truncated, which would hand the user an incomplete manual that looks complete.
    /// </summary>
    public int MaxExportPages { get; set; } = 200;
}
