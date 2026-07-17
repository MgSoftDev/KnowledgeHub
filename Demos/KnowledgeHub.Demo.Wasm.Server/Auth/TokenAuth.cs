using System.Collections.Concurrent;
using KnowledgeHub.Demo.SharedAuth;
using MgSoftDev.KnowledgeHub.Contracts;
using MgSoftDev.KnowledgeHub.Security;
using MgSoftDev.ReturningCore;
using MgSoftDev.ReturningCore.Exceptions;
using MgSoftDev.ReturningCore.Helper;

namespace KnowledgeHub.Demo.Wasm.Server.Auth;

/// <summary>
/// Demo token store: opaque bearer tokens held in memory (restart clears every session).
/// A real host would issue JWTs or reference tokens with expiry.
/// </summary>
public sealed class TokenStore
{
    private readonly ConcurrentDictionary<string, DemoUserDto> _sessions = new();

    public string Issue(DemoUserDto user)
    {
        var token = Convert.ToBase64String(Guid.NewGuid().ToByteArray())
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
        _sessions[token] = user;
        return token;
    }

    public DemoUserDto? Resolve(string token) =>
        _sessions.TryGetValue(token, out var user) ? user : null;

    public void Revoke(string token) => _sessions.TryRemove(token, out _);
}

/// <summary>
/// Per-request user context resolved from the Authorization: Bearer header. This is the
/// server-side AUTHORITY: every KnowledgeHub service call is guarded against it, regardless
/// of what the WASM client shows in its UI.
/// </summary>
public sealed class RequestUserContext : IKnowledgeHubUserContext
{
    private readonly DemoAdminService _adminService;
    private readonly DemoUserDto? _user;
    private readonly IReadOnlyList<string> _permissions;

    public RequestUserContext(IHttpContextAccessor accessor, TokenStore tokens, DemoAdminService adminService)
    {
        _adminService = adminService;

        var header = accessor.HttpContext?.Request.Headers.Authorization.ToString();
        if (header is not null && header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            _user = tokens.Resolve(header["Bearer ".Length..].Trim());

        _permissions = _user is null ? Array.Empty<string>() : DemoPermissions.ForRoles(_user.Roles);
    }

    public bool IsAuthenticated => _user is not null;
    public string UserName => _user?.UserName ?? string.Empty;
    public string DisplayName => _user?.FullName ?? string.Empty;
    public IReadOnlyList<string> Permissions => _permissions;

    public Task<ReturningList<PermissionInfo>> GetPermissionCatalogAsync() =>
        ReturningList<PermissionInfo>.TryTask(async () =>
        {
            var roles = await _adminService.GetRolesAsync();
            if (!roles.Ok) roles.Throw();
            return roles.Value!.Select(DemoPermissions.ToCatalogEntry).ToList();
        }, saveLog: true);
}
