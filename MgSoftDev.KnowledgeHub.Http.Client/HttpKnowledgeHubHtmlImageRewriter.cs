using MgSoftDev.KnowledgeHub.Contracts;
using MgSoftDev.KnowledgeHub.Dtos;
using MgSoftDev.KnowledgeHub.Transport;
using MgSoftDev.ReturningCore;

namespace MgSoftDev.KnowledgeHub.Http.Client;

/// <summary>
/// IKnowledgeHubHtmlImageRewriter over the KnowledgeHub HTTP API: the SERVER resolves the
/// display URLs (and warms its disk cache); the client just renders them and lets the browser
/// cache the immutable image responses.
/// </summary>
public sealed class HttpKnowledgeHubHtmlImageRewriter : IKnowledgeHubHtmlImageRewriter
{
    private readonly KnowledgeHubApiClient _api;

    public HttpKnowledgeHubHtmlImageRewriter(KnowledgeHubApiClient api)
    {
        _api = api;
    }

    public Task<Returning<HtmlRewriteResult>> PrepareForDisplayAsync(string storedHtml) =>
        _api.PostAsync<HtmlRewriteResult>("/html/prepare", new PrepareHtmlRequest(storedHtml));
}
