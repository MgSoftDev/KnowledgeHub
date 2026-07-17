using MgSoftDev.KnowledgeHub.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace MgSoftDev.KnowledgeHub.AspNetCore;

public static class KnowledgeHubAssetsEndpoints
{
    /// <summary>
    /// Maps GET <c>{pattern}/{contentHash}.webp</c>, serving image binaries by content hash.
    /// Hash-addressed content never changes, so responses carry
    /// <c>Cache-Control: public, max-age=31536000, immutable</c> — after the first load the
    /// browser cache takes over. When an <see cref="IKnowledgeHubImageCache"/> is registered
    /// the endpoint works cache-aside against the server's disk; otherwise it streams from the
    /// store. The pattern must match KnowledgeHubOptions.PublicAssetsBaseUrl.
    /// </summary>
    public static IEndpointConventionBuilder MapKnowledgeHubAssets(this IEndpointRouteBuilder endpoints,
        string pattern = "/kh/assets")
    {
        var route = pattern.TrimEnd('/') + "/{fileName}";

        return endpoints.MapGet(route, async (string fileName, HttpContext http) =>
        {
            // Strictly {64-hex-hash}.webp — anything else is not ours.
            if (fileName.Length != 69 || !fileName.EndsWith(".webp", StringComparison.OrdinalIgnoreCase))
                return Results.NotFound();

            var hash = fileName[..64].ToLowerInvariant();
            if (!hash.All(Uri.IsHexDigit))
                return Results.NotFound();

            http.Response.Headers.CacheControl = "public, max-age=31536000, immutable";

            var store = http.RequestServices.GetRequiredService<IKnowledgeHubStore>();
            var cache = http.RequestServices.GetService<IKnowledgeHubImageCache>();

            // Cache-aside on the server disk when a local cache is available.
            if (cache is not null)
            {
                var path = Path.Combine(cache.CacheFolder, hash + ".webp");
                if (!File.Exists(path))
                {
                    // Defensive pattern-match: a third-party store could return a null Returning.
                    var blob = await store.GetImageContentByHashAsync(hash);
                    if (blob is not { Ok: true, Value: not null }) return Results.NotFound();
                    await File.WriteAllBytesAsync(path, blob.Value.Content);
                }
                return Results.File(path, "image/webp");
            }

            var direct = await store.GetImageContentByHashAsync(hash);
            if (direct is not { Ok: true, Value: not null }) return Results.NotFound();
            return Results.Bytes(direct.Value.Content, "image/webp");
        });
    }
}
