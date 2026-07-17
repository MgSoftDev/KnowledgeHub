using System.Diagnostics;
using MgSoftDev.KnowledgeHub.Contracts;
using MgSoftDev.KnowledgeHub.Dtos;
using MgSoftDev.ReturningCore;
using MgSoftDev.ReturningCore.Exceptions;
using MgSoftDev.ReturningCore.Helper;

namespace MgSoftDev.KnowledgeHub.Imaging;

/// <summary>
/// Hash-addressed local disk cache. Files are named by pure content hash, which yields
/// automatic deduplication and automatic invalidation (a replaced image has a new hash → a
/// new file name → a guaranteed miss). There is no time-based expiration. Missing binaries
/// are always fetched from the store in a single batch.
/// </summary>
public sealed class FileSystemImageCache : IKnowledgeHubImageCache
{
    private readonly IKnowledgeHubStore _store;

    public string CacheFolder { get; }

    public FileSystemImageCache(IKnowledgeHubStore store, string? cacheFolder = null)
    {
        _store = store;
        CacheFolder = string.IsNullOrWhiteSpace(cacheFolder)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "KnowledgeHub", "cache")
            : cacheFolder;
        Directory.CreateDirectory(CacheFolder);
    }

    public Task<Returning<CacheEnsureResult>> EnsureCachedAsync(IReadOnlyCollection<ImageRefDto> images) =>
        Returning<CacheEnsureResult>.TryTask(async () =>
        {
            var result = new CacheEnsureResult();
            if (images.Count == 0) return result;

            // Disk check per image.
            var missing = new List<ImageRefDto>();
            foreach (var image in images)
            {
                if (File.Exists(Path.Combine(CacheFolder, image.ContentHash + ".webp")))
                    result.CacheHits++;
                else
                {
                    result.CacheMisses++;
                    missing.Add(image);
                }
            }

            // Only if needed: all missing binaries in a single batch.
            if (missing.Count > 0)
            {
                var sw = Stopwatch.StartNew();
                var blobsR = await _store.GetImageContentsAsync(missing.Select(m => m.Pk).ToList());
                sw.Stop();
                if (!blobsR.Ok) blobsR.Throw();
                result.BlobsQueryMs = sw.Elapsed.TotalMilliseconds;

                var hashById = missing.ToDictionary(m => m.Pk, m => m.ContentHash);
                foreach (var blob in blobsR.Value!)
                {
                    result.BytesFromStore += blob.Content.LongLength;
                    var path = Path.Combine(CacheFolder, hashById[blob.ImagePk] + ".webp");
                    await File.WriteAllBytesAsync(path, blob.Content);
                }
            }

            return result;
        }, saveLog: true);

    public Returning ClearCache() =>
        Returning.Try(() =>
        {
            if (Directory.Exists(CacheFolder))
                foreach (var file in Directory.EnumerateFiles(CacheFolder, "*.webp"))
                    File.Delete(file);
            return Returning.Success();
        }).SaveLog();

    public long GetCacheFolderSizeBytes()
    {
        if (!Directory.Exists(CacheFolder)) return 0;
        return Directory.EnumerateFiles(CacheFolder, "*.webp").Sum(f => new FileInfo(f).Length);
    }

    public Task<Returning<int>> CleanupOrphansAsync() =>
        Returning<int>.TryTask(async () =>
        {
            var hashesR = await _store.GetAllImageHashesAsync();
            if (!hashesR.Ok) hashesR.Throw();
            var validHashes = hashesR.Value!.ToHashSet(StringComparer.OrdinalIgnoreCase);

            var removed = 0;
            if (Directory.Exists(CacheFolder))
            {
                foreach (var file in Directory.EnumerateFiles(CacheFolder, "*.webp"))
                {
                    var hash = Path.GetFileNameWithoutExtension(file);
                    if (!validHashes.Contains(hash))
                    {
                        File.Delete(file);
                        removed++;
                    }
                }
            }
            return removed;
        }, saveLog: true);
}
