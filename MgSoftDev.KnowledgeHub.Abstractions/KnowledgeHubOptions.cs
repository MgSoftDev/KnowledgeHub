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
}
