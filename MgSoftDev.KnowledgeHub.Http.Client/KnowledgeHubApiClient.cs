using System.Net.Http.Json;
using MgSoftDev.KnowledgeHub.Transport;
using MgSoftDev.ReturningCore;
using MgSoftDev.ReturningCore.Exceptions;
using MgSoftDev.ReturningCore.Helper;

namespace MgSoftDev.KnowledgeHub.Http.Client;

/// <summary>
/// Thin typed wrapper over the KnowledgeHub HTTP API: GET/POST/DELETE returning ApiResult
/// payloads mapped back into Returning results. Transport failures (network, non-2xx) become
/// technical errors; business outcomes arrive inside the payload.
/// </summary>
public sealed class KnowledgeHubApiClient
{
    private readonly HttpClient _http;
    private readonly string _basePath;

    public KnowledgeHubApiClient(HttpClient http, KnowledgeHubHttpClientOptions options)
    {
        _http = http;
        _basePath = options.ApiBasePath.TrimEnd('/');
    }

    public HttpClient Http => _http;
    public string BasePath => _basePath;

    public Task<Returning<T>> GetAsync<T>(string relative) =>
        Returning<T>.TryTask(async () =>
        {
            var response = await _http.GetAsync(_basePath + relative);
            response.EnsureSuccessStatusCode();
            var api = await response.Content.ReadFromJsonAsync<ApiResult<T>>();
            return api.ToReturning();
        }, saveLog: true);

    public Task<ReturningList<T>> GetListAsync<T>(string relative) =>
        ReturningList<T>.TryTask(async () =>
        {
            var response = await _http.GetAsync(_basePath + relative);
            response.EnsureSuccessStatusCode();
            var api = await response.Content.ReadFromJsonAsync<ApiResult<List<T>>>();
            return api.ToReturningList();
        }, saveLog: true);

    public Task<Returning<T>> PostAsync<T>(string relative, object? body) =>
        Returning<T>.TryTask(async () =>
        {
            var response = await _http.PostAsJsonAsync(_basePath + relative, body);
            response.EnsureSuccessStatusCode();
            var api = await response.Content.ReadFromJsonAsync<ApiResult<T>>();
            return api.ToReturning();
        }, saveLog: true);

    public Task<Returning> PostPlainAsync(string relative, object? body) =>
        Returning.TryTask(async () =>
        {
            var response = await _http.PostAsJsonAsync(_basePath + relative, body);
            response.EnsureSuccessStatusCode();
            var api = await response.Content.ReadFromJsonAsync<ApiResult<bool>>();
            return api.ToPlainReturning();
        }, saveLog: true);

    public Task<Returning> DeleteAsync(string relative) =>
        Returning.TryTask(async () =>
        {
            var response = await _http.DeleteAsync(_basePath + relative);
            response.EnsureSuccessStatusCode();
            var api = await response.Content.ReadFromJsonAsync<ApiResult<bool>>();
            return api.ToPlainReturning();
        }, saveLog: true);
}
