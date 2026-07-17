using System.Security.Claims;
using KnowledgeHub.Demo.SharedAuth;
using MgSoftDev.KnowledgeHub.Contracts;
using MgSoftDev.KnowledgeHub.Security;
using MgSoftDev.ReturningCore;
using MgSoftDev.ReturningCore.Exceptions;
using MgSoftDev.ReturningCore.Helper;
using Microsoft.AspNetCore.Components.Authorization;

namespace KnowledgeHub.Demo.BlazorServer.Auth;

/// <summary>
/// Per-circuit implementation of <see cref="IKnowledgeHubUserContext"/>: each Blazor Server
/// circuit is one signed-in browser, so this scoped service reads the cookie principal from
/// the circuit's <see cref="AuthenticationStateProvider"/> (already resolved when the circuit
/// starts). Role claims map to KnowledgeHub permissions through <see cref="DemoPermissions"/>.
/// </summary>
public sealed class ServerUserContext : IKnowledgeHubUserContext
{
    private readonly AuthenticationStateProvider _authState;
    private readonly DemoAdminService _adminService;
    private ClaimsPrincipal? _principal;
    private IReadOnlyList<string>? _permissions;

    public ServerUserContext(AuthenticationStateProvider authState, DemoAdminService adminService)
    {
        _authState = authState;
        _adminService = adminService;
    }

    private ClaimsPrincipal Principal =>
        // In Blazor Server the provider returns an already-completed task seeded from the
        // connection's cookie principal, so reading it synchronously is safe.
        _principal ??= _authState.GetAuthenticationStateAsync().GetAwaiter().GetResult().User;

    public bool IsAuthenticated => Principal.Identity?.IsAuthenticated == true;

    public string UserName => Principal.Identity?.Name ?? string.Empty;

    public string DisplayName => Principal.FindFirst("FullName")?.Value ?? UserName;

    public IReadOnlyList<string> Permissions =>
        _permissions ??= IsAuthenticated
            ? DemoPermissions.ForRoles(Principal.FindAll(ClaimTypes.Role).Select(c => c.Value))
            : Array.Empty<string>();

    public Task<ReturningList<PermissionInfo>> GetPermissionCatalogAsync() =>
        ReturningList<PermissionInfo>.TryTask(async () =>
        {
            var roles = await _adminService.GetRolesAsync();
            if (!roles.Ok) roles.Throw();
            return roles.Value!.Select(DemoPermissions.ToCatalogEntry).ToList();
        }, saveLog: true);
}
