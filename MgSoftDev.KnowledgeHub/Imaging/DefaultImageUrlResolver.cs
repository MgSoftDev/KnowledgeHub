using MgSoftDev.KnowledgeHub.Contracts;

namespace MgSoftDev.KnowledgeHub.Imaging;

/// <summary>
/// Builds display URLs as <c>{PublicAssetsBaseUrl}/{hash}.webp</c>. The base URL comes from
/// <see cref="KnowledgeHubOptions.PublicAssetsBaseUrl"/>: a WebView2 virtual host in WPF, a
/// relative endpoint in Blazor Server, or an absolute API endpoint for WASM clients.
/// </summary>
public sealed class DefaultImageUrlResolver : IKnowledgeHubImageUrlResolver
{
    private readonly string _baseUrl;

    public DefaultImageUrlResolver(string baseUrl)
    {
        _baseUrl = baseUrl.TrimEnd('/');
    }

    public string GetImageUrl(string contentHash) => $"{_baseUrl}/{contentHash}.webp";
}
