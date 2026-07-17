using MgSoftDev.KnowledgeHub.Contracts;
using MgSoftDev.KnowledgeHub.Security;
using MgSoftDev.ReturningCore;
using MgSoftDev.ReturningCore.Exceptions;
using MgSoftDev.ReturningCore.Helper;

namespace KnowledgeHub.ParityHarness;

/// <summary>Mutable fake host user context so the script can switch users mid-run.</summary>
public sealed class HarnessUserContext : IKnowledgeHubUserContext
{
    private string[] _permissions = Array.Empty<string>();

    public bool IsAuthenticated { get; private set; }
    public string UserName { get; private set; } = string.Empty;
    public string DisplayName { get; private set; } = string.Empty;
    public IReadOnlyList<string> Permissions => _permissions;

    /// <summary>Catalog offered to the visibility picker.</summary>
    public List<PermissionInfo> Catalog { get; } = new()
    {
        new("Docs.Tech", "Documentación técnica"),
        new("Docs.Prod", "Producción"),
        new("Docs.Ofi", "Oficinas")
    };

    public void SetUser(string userName, string displayName, params string[] permissions)
    {
        UserName = userName;
        DisplayName = displayName;
        _permissions = permissions;
        IsAuthenticated = true;
    }

    public Task<ReturningList<PermissionInfo>> GetPermissionCatalogAsync() =>
        Task.FromResult(ReturningList<PermissionInfo>.Try(() => Catalog.ToList()));
}
