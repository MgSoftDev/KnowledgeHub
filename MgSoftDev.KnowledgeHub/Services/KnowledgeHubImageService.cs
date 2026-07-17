using System.Security.Cryptography;
using MgSoftDev.KnowledgeHub.Contracts;
using MgSoftDev.KnowledgeHub.Entities;
using MgSoftDev.KnowledgeHub.Security;
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

            // Decode, cap the width, re-encode as WebP.
            using var image = Image.Load(originalBytes);
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
}
