using MgSoftDev.KnowledgeHub.Contracts;
using MgSoftDev.KnowledgeHub.Dtos;
using MgSoftDev.KnowledgeHub.Transport;
using MgSoftDev.ReturningCore;

namespace MgSoftDev.KnowledgeHub.Http.Client;

/// <summary>IKnowledgeHubPageService over the KnowledgeHub HTTP API.</summary>
public sealed class HttpKnowledgeHubPageService : IKnowledgeHubPageService
{
    private readonly KnowledgeHubApiClient _api;

    public HttpKnowledgeHubPageService(KnowledgeHubApiClient api)
    {
        _api = api;
    }

    public Task<ReturningList<PageTreeNodeDto>> GetTreeAsync() =>
        _api.GetListAsync<PageTreeNodeDto>("/pages/tree");

    public Task<Returning<PageReadDto>> GetPageForReadAsync(Guid pagePk) =>
        _api.GetAsync<PageReadDto>($"/pages/{pagePk}/read");

    public Task<Returning<PageEditDto>> GetPageForEditAsync(Guid pagePk) =>
        _api.GetAsync<PageEditDto>($"/pages/{pagePk}/edit");

    public Task<Returning<PageReadDto>> GetVersionContentAsync(Guid versionPk) =>
        _api.GetAsync<PageReadDto>($"/versions/{versionPk}");

    public Task<Returning<int>> SaveDraftAsync(PageEditDto draft) =>
        _api.PostAsync<int>("/pages/draft", draft);

    public Task<Returning> PublishAsync(Guid pagePk, int baseVersionNumber) =>
        _api.PostPlainAsync($"/pages/{pagePk}/publish", new PublishPageRequest(baseVersionNumber));

    public Task<Returning> RestoreVersionAsync(Guid versionPk) =>
        _api.PostPlainAsync($"/versions/{versionPk}/restore", body: null);

    public Task<ReturningList<VersionListItemDto>> GetVersionsAsync(Guid pagePk) =>
        _api.GetListAsync<VersionListItemDto>($"/pages/{pagePk}/versions");

    public Task<Returning<PageInfoDto>> GetPageInfoAsync(Guid pagePk) =>
        _api.GetAsync<PageInfoDto>($"/pages/{pagePk}/info");

    public Task<Returning<Guid>> CreatePageAsync(Guid? parentPk, string title, string slug) =>
        _api.PostAsync<Guid>("/pages", new CreatePageRequest(parentPk, title, slug));

    public Task<Returning> RenamePageAsync(Guid pagePk, string title) =>
        _api.PostPlainAsync($"/pages/{pagePk}/rename", new RenamePageRequest(title));

    public Task<Returning> MovePageAsync(Guid pagePk, Guid? newParentPk) =>
        _api.PostPlainAsync($"/pages/{pagePk}/move", new MovePageRequest(newParentPk));

    public Task<Returning> ReorderAsync(Guid pagePk, int sortOrder) =>
        _api.PostPlainAsync($"/pages/{pagePk}/reorder", new ReorderPageRequest(sortOrder));

    public Task<Returning> SetPageIconAsync(Guid pagePk, string? icon, string? iconColor) =>
        _api.PostPlainAsync($"/pages/{pagePk}/icon", new SetIconRequest(icon, iconColor));

    public Task<Returning> DeletePageAsync(Guid pagePk) =>
        _api.DeleteAsync($"/pages/{pagePk}");

    public Task<Returning<PagePermissionsDto>> GetPermissionsAsync(Guid pagePk) =>
        _api.GetAsync<PagePermissionsDto>($"/pages/{pagePk}/permissions");

    public Task<Returning> SetPermissionsAsync(Guid pagePk, bool isPublic, IReadOnlyList<string> permissions) =>
        _api.PostPlainAsync($"/pages/{pagePk}/permissions", new SetPermissionsRequest(isPublic, permissions.ToList()));

    public Task<ReturningList<SearchResultDto>> SearchAsync(string term) =>
        _api.GetListAsync<SearchResultDto>($"/search?term={Uri.EscapeDataString(term)}");
}
