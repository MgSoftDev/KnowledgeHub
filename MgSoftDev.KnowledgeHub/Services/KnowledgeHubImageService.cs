using System.Security.Cryptography;
using MgSoftDev.KnowledgeHub.Contracts;
using MgSoftDev.KnowledgeHub.Dtos;
using MgSoftDev.KnowledgeHub.Entities;
using MgSoftDev.KnowledgeHub.Security;
using MgSoftDev.KnowledgeHub.Store;
using MgSoftDev.ReturningCore;
using MgSoftDev.ReturningCore.Exceptions;
using MgSoftDev.ReturningCore.Helper;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;

namespace MgSoftDev.KnowledgeHub.Services;

/// <summary>
/// Converts uploaded/pasted images to WebP (width capped via options), hashes them, and stores
/// metadata and binary through the store — reusing an existing row when the hash already exists.
/// </summary>
public sealed class KnowledgeHubImageService : IKnowledgeHubImageService
{
    private const string AdminOnlyMessage = "Solo un administrador puede hacer mantenimiento de imágenes";

    private readonly IKnowledgeHubStore _store;
    private readonly IKnowledgeHubUserContext _user;
    private readonly KnowledgeHubOptions _options;

    public KnowledgeHubImageService(IKnowledgeHubStore store, IKnowledgeHubUserContext user, KnowledgeHubOptions options)
    {
        _store = store;
        _user = user;
        _options = options;
    }

    public Task<Returning<Guid>> UploadOrReplaceAsync(byte[] originalBytes, string fileName) =>
        Returning<Guid>.TryTask(async () =>
        {
            if (!_user.CanEdit())
                return Returning.Unfinished("No tienes permiso para realizar esta acción", UnfinishedInfo.NotifyType.Warning);
            if (originalBytes is null || originalBytes.Length == 0)
                return Returning.Unfinished("La imagen está vacía", UnfinishedInfo.NotifyType.Warning);

            // Decode, cap the width, re-encode as WebP. An unsupported format (SVG…) or corrupt
            // content is a BUSINESS rejection, not an infrastructure error: callers must be able
            // to tell it apart from a store failure, so it must not surface as an Error.
            Image loaded;
            try
            {
                loaded = Image.Load(originalBytes);
            }
            catch (ImageFormatException)
            {
                return Returning.Unfinished("Formato de imagen no soportado o archivo dañado",
                    UnfinishedInfo.NotifyType.Warning);
            }

            using var image = loaded;
            var maxWidth = _options.MaxImageWidth;
            if (image.Width > maxWidth)
            {
                var newHeight = (int)Math.Round(image.Height * (maxWidth / (double)image.Width));
                image.Mutate(x => x.Resize(maxWidth, newHeight));
            }

            using var ms = new MemoryStream();
            await image.SaveAsWebpAsync(ms, new WebpEncoder());
            var webp = ms.ToArray();

            var hash = Convert.ToHexString(SHA256.HashData(webp)).ToLowerInvariant();

            // Deduplicate: identical binary → identical hash → reuse the existing image.
            var existingR = await _store.GetImageRefsByHashesAsync(new[] { hash });
            if (!existingR.Ok) existingR.Throw();
            if (existingR.Value!.FirstOrDefault() is { } existing)
                return existing.Pk;

            var docImage = new DocImage
            {
                FileName = string.IsNullOrWhiteSpace(fileName) ? "image.webp" : fileName,
                ContentHash = hash,
                ContentType = "image/webp",
                SizeBytes = webp.LongLength,
                Width = image.Width,
                Height = image.Height
            };
            EntityStamp.PrepareNew(docImage, _user.UserName, DateTime.Now);

            var insertR = await _store.InsertImageAsync(docImage, webp);
            if (!insertR.Ok) insertR.Throw();

            return docImage.Pk;
        }, saveLog: true);

    public Task<Returning<OrphanImageReportDto>> AnalyzeOrphanImagesAsync() =>
        Returning<OrphanImageReportDto>.TryTask(async () =>
        {
            if (!_user.IsAdmin())
                return Returning.Unfinished(AdminOnlyMessage, UnfinishedInfo.NotifyType.Warning);

            var (all, orphans) = await FindOrphansAsync();

            return new OrphanImageReportDto
            {
                TotalImages = all.Count,
                ReferencedImages = all.Count - orphans.Count,
                OrphanImages = orphans.Count,
                OrphanBytes = orphans.Sum(i => i.SizeBytes),
                SampleFileNames = orphans.Take(10).Select(i => i.FileName).ToList()
            };
        }, saveLog: true);

    public Task<Returning<int>> DeleteOrphanImagesAsync() =>
        Returning<int>.TryTask(async () =>
        {
            if (!_user.IsAdmin())
                return Returning.Unfinished(AdminOnlyMessage, UnfinishedInfo.NotifyType.Warning);

            // Recomputed here on purpose: deleting off a report the caller obtained earlier would
            // remove images a page saved in the meantime is already using.
            var (_, orphans) = await FindOrphansAsync();
            if (orphans.Count == 0) return 0;

            var deletedR = await _store.DeleteImagesAsync(
                orphans.Select(i => i.Pk).ToList(),
                new AuditStamp(_user.UserName, DateTime.Now));
            if (!deletedR.Ok) deletedR.Throw();

            return deletedR.Value;
        }, saveLog: true);

    /// <summary>
    /// Every stored image minus the ones any version still references. The reference set covers
    /// the FULL history, so restoring an old version never finds a missing image.
    /// </summary>
    private async Task<(List<ImageSummaryDto> All, List<ImageSummaryDto> Orphans)> FindOrphansAsync()
    {
        var allR = await _store.GetAllImageSummariesAsync();
        if (!allR.Ok) allR.Throw();

        var referencedR = await _store.GetReferencedImagePksAsync();
        if (!referencedR.Ok) referencedR.Throw();

        var referenced = referencedR.Value!.ToHashSet();
        var all = allR.Value!;
        return (all, all.Where(i => !referenced.Contains(i.Pk)).ToList());
    }
}
