using KnowledgeHub.Demo.SharedAuth;
using MgSoftDev.KnowledgeHub.Contracts;
using MgSoftDev.KnowledgeHub.Security;
using MgSoftDev.ReturningCore;
using MgSoftDev.ReturningCore.Exceptions;
using MgSoftDev.ReturningCore.Helper;

namespace KnowledgeHub.Demo.Wpf.Auth;

/// <summary>
/// The demo host's implementation of <see cref="IKnowledgeHubUserContext"/>. Singleton and
/// mutable because a WPF desktop process has exactly one signed-in user; the login window
/// calls <see cref="SetUser"/>. Role names map to KnowledgeHub permission strings through
/// <see cref="DemoPermissions"/>.
/// </summary>
public sealed class DemoUserContext : IKnowledgeHubUserContext
{
    private readonly DemoAdminService _adminService;
    private DemoUserDto? _user;
    private IReadOnlyList<string> _permissions = Array.Empty<string>();

    public DemoUserContext(DemoAdminService adminService)
    {
        _adminService = adminService;
    }

    public bool IsAuthenticated => _user is not null;
    public string UserName => _user?.UserName ?? string.Empty;
    public string DisplayName => _user?.FullName ?? string.Empty;
    public IReadOnlyList<string> Permissions => _permissions;

    /// <summary>Roles of the current host user (for the status bar).</summary>
    public IReadOnlyList<string> Roles => _user?.Roles ?? Array.Empty<string>();

    public Task<ReturningList<PermissionInfo>> GetPermissionCatalogAsync() =>
        ReturningList<PermissionInfo>.TryTask(async () =>
        {
            var roles = await _adminService.GetRolesAsync();
            if (!roles.Ok) roles.Throw();
            return roles.Value!.Select(DemoPermissions.ToCatalogEntry).ToList();
        }, saveLog: true);

    public void SetUser(DemoUserDto user)
    {
        _user = user;
        _permissions = DemoPermissions.ForRoles(user.Roles);
    }

    public void Clear()
    {
        _user = null;
        _permissions = Array.Empty<string>();
    }
}
