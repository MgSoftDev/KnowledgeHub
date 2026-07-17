using System.Net.Http.Json;
using MgSoftDev.KnowledgeHub.Contracts;
using MgSoftDev.KnowledgeHub.Security;
using MgSoftDev.KnowledgeHub.Transport;
using MgSoftDev.ReturningCore;
using MgSoftDev.ReturningCore.Exceptions;
using MgSoftDev.ReturningCore.Helper;

namespace KnowledgeHub.Demo.Wasm.Auth;

/// <summary>
/// Client-side user context, populated from GET /kh/api/me after login. The UI reads it to
/// show/hide actions; the REAL authority stays on the server, which re-resolves the user per
/// request from the bearer token and guards every mutation.
/// </summary>
public sealed class ClientUserContext : IKnowledgeHubUserContext
{
    private MeResponse _me = new();

    public event Action? Changed;

    public bool IsAuthenticated => _me.IsAuthenticated;
    public string UserName => _me.UserName;
    public string DisplayName => _me.DisplayName;
    public IReadOnlyList<string> Permissions => _me.Permissions;

    public Task<ReturningList<PermissionInfo>> GetPermissionCatalogAsync() =>
        Task.FromResult(ReturningList<PermissionInfo>.Try(() => _me.Catalog.ToList()));

    /// <summary>Refreshes the local identity snapshot from the server (/kh/api/me).</summary>
    public Task<Returning> RefreshAsync(HttpClient http) =>
        Returning.TryTask(async () =>
        {
            var me = await http.GetFromJsonAsync<MeResponse>("/kh/api/me");
            _me = me ?? new MeResponse();
            Changed?.Invoke();
            return Returning.Success();
        }, saveLog: true);

    public void Clear()
    {
        _me = new MeResponse();
        Changed?.Invoke();
    }
}
