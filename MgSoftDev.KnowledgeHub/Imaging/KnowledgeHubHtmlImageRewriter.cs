using System.Diagnostics;
using MgSoftDev.KnowledgeHub.Contracts;
using MgSoftDev.KnowledgeHub.Dtos;
using MgSoftDev.ReturningCore;
using MgSoftDev.ReturningCore.Exceptions;
using MgSoftDev.ReturningCore.Helper;

namespace MgSoftDev.KnowledgeHub.Imaging;

/// <summary>
/// Default in-process rewriter: resolves the referenced images (Pk + hash only, never the
/// binary), optionally ensures they are locally cached, and rewrites <c>docimg://{pk}</c> to
/// the display URL of the current hosting model. WASM clients use the HTTP implementation
/// from the Http.Client package instead.
/// </summary>
public sealed class KnowledgeHubHtmlImageRewriter : IKnowledgeHubHtmlImageRewriter
{
    private readonly IKnowledgeHubStore _store;
    private readonly IKnowledgeHubImageUrlResolver _urlResolver;
    private readonly IKnowledgeHubImageCache? _cache;

    public KnowledgeHubHtmlImageRewriter(IKnowledgeHubStore store, IKnowledgeHubImageUrlResolver urlResolver,
        IKnowledgeHubImageCache? cache = null)
    {
        _store = store;
        _urlResolver = urlResolver;
        _cache = cache;
    }

    public Task<Returning<HtmlRewriteResult>> PrepareForDisplayAsync(string storedHtml) =>
        Returning<HtmlRewriteResult>.TryTask(async () =>
        {
            var result = new HtmlRewriteResult { Html = storedHtml };
            if (string.IsNullOrEmpty(storedHtml)) return result;

            var ids = KnowledgeHubHtml.ExtractDocImagePks(storedHtml);
            result.ImageCount = ids.Count;
            if (ids.Count == 0) return result;

            // Only Pk + ContentHash (never the binary).
            var sw = Stopwatch.StartNew();
            var refsR = await _store.GetImageRefsAsync(ids.ToList());
            sw.Stop();
            if (!refsR.Ok) refsR.Throw();
            var refs = refsR.Value!;
            result.HashesQueryMs = sw.Elapsed.TotalMilliseconds;

            // With a local cache registered, make sure every binary is on disk (single batch).
            if (_cache is not null)
            {
                var ensureR = await _cache.EnsureCachedAsync(refs);
                if (!ensureR.Ok) ensureR.Throw();
                var ensure = ensureR.Value!;
                result.CacheHits = ensure.CacheHits;
                result.CacheMisses = ensure.CacheMisses;
                result.BlobsQueryMs = ensure.BlobsQueryMs;
                result.BytesFromStore = ensure.BytesFromStore;
            }

            // Rewrite each docimg://{pk} to its display URL.
            var map = refs.ToDictionary(r => r.Pk, r => r.ContentHash);
            result.Html = KnowledgeHubHtml.DocImgRegex().Replace(storedHtml, m =>
            {
                var pk = Guid.Parse(m.Groups["pk"].Value);
                return map.TryGetValue(pk, out var hash) ? _urlResolver.GetImageUrl(hash) : m.Value;
            });

            return result;
        }, saveLog: true);
}
