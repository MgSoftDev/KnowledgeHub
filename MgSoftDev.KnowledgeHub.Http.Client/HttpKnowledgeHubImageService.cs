using MgSoftDev.KnowledgeHub.Contracts;
using MgSoftDev.KnowledgeHub.Transport;
using MgSoftDev.ReturningCore;

namespace MgSoftDev.KnowledgeHub.Http.Client;

/// <summary>IKnowledgeHubImageService over the KnowledgeHub HTTP API.</summary>
public sealed class HttpKnowledgeHubImageService : IKnowledgeHubImageService
{
    private readonly KnowledgeHubApiClient _api;

    public HttpKnowledgeHubImageService(KnowledgeHubApiClient api)
    {
        _api = api;
    }

    public Task<Returning<Guid>> UploadOrReplaceAsync(byte[] originalBytes, string fileName) =>
        _api.PostAsync<Guid>("/images", new UploadImageRequest(fileName, Convert.ToBase64String(originalBytes)));
}
