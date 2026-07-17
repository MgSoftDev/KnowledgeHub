using MgSoftDev.ReturningCore;
using MgSoftDev.ReturningCore.Exceptions;
using MgSoftDev.ReturningCore.Helper;
using Microsoft.AspNetCore.Identity;

namespace KnowledgeHub.Demo.SharedAuth;

/// <summary>Administration of the demo host's users and roles.</summary>
public sealed class DemoAdminService
{
    private readonly DemoAuthContext _ctx;
    private readonly IPasswordHasher<DemoUser> _hasher;

    public DemoAdminService(DemoAuthContext ctx, IPasswordHasher<DemoUser> hasher)
    {
        _ctx = ctx;
        _hasher = hasher;
    }

    public Task<ReturningList<DemoRoleDto>> GetRolesAsync() =>
        Task.FromResult(ReturningList<DemoRoleDto>.Try(() =>
            _ctx.Roles.Query().ToList()
                .Where(r => r.IsActive)
                .OrderBy(r => r.Name)
                .Select(r => new DemoRoleDto { Pk = r.Pk, Name = r.Name, Description = r.Description })
                .ToList()));

    public Task<ReturningList<DemoUserAdminDto>> GetUsersAsync() =>
        Task.FromResult(ReturningList<DemoUserAdminDto>.Try(() =>
        {
            var roleNames = _ctx.Roles.Query().ToList().ToDictionary(r => r.Pk, r => r.Name);
            return _ctx.Users.Query().ToList()
                .OrderBy(u => u.UserName)
                .Select(u => new DemoUserAdminDto
                {
                    Pk = u.Pk,
                    UserName = u.UserName,
                    FullName = u.FullName,
                    IsActive = u.IsActive,
                    RoleIds = u.RoleIds,
                    RolesText = string.Join(", ", u.RoleIds
                        .Where(roleNames.ContainsKey)
                        .Select(pk => roleNames[pk]))
                })
                .ToList();
        }));

    public Task<Returning<Guid>> CreateRoleAsync(string name, string? description) =>
        Task.FromResult(Returning<Guid>.Try(() =>
        {
            if (string.IsNullOrWhiteSpace(name))
                return Returning.Unfinished("El nombre del rol es requerido", UnfinishedInfo.NotifyType.Warning);
            if (_ctx.Roles.Query().ToList().Any(r => string.Equals(r.Name, name, StringComparison.OrdinalIgnoreCase)))
                return Returning.Unfinished("Ya existe un rol con ese nombre", UnfinishedInfo.NotifyType.Warning);

            var role = new DemoRole { Pk = Guid.CreateVersion7(), Name = name.Trim(), Description = description };
            _ctx.Roles.Insert(role);
            return role.Pk;
        }).SaveLog());

    public Task<Returning<Guid>> CreateUserAsync(string userName, string fullName, string password,
        IReadOnlyList<Guid> roleIds) =>
        Task.FromResult(Returning<Guid>.Try(() =>
        {
            if (string.IsNullOrWhiteSpace(userName) || string.IsNullOrWhiteSpace(fullName) ||
                string.IsNullOrWhiteSpace(password))
                return Returning.Unfinished("Usuario, nombre y contraseña son requeridos", UnfinishedInfo.NotifyType.Warning);
            if (_ctx.Users.Query().ToList().Any(u => string.Equals(u.UserName, userName, StringComparison.OrdinalIgnoreCase)))
                return Returning.Unfinished("Ya existe un usuario con ese nombre", UnfinishedInfo.NotifyType.Warning);

            var user = new DemoUser
            {
                Pk = Guid.CreateVersion7(),
                UserName = userName.Trim(),
                FullName = fullName.Trim(),
                PasswordHash = _hasher.HashPassword(new DemoUser(), password),
                RoleIds = roleIds.Distinct().ToList()
            };
            _ctx.Users.Insert(user);
            return user.Pk;
        }).SaveLog());

    public Task<Returning> UpdateUserRolesAsync(Guid userPk, IReadOnlyList<Guid> roleIds) =>
        Task.FromResult(Returning.Try(() =>
        {
            var user = _ctx.Users.FindById(userPk);
            if (user is null)
                return Returning.Unfinished("Usuario no encontrado", UnfinishedInfo.NotifyType.Warning);

            user.RoleIds = roleIds.Distinct().ToList();
            _ctx.Users.Update(user);
            return Returning.Success();
        }).SaveLog());

    public Task<Returning> SetUserActiveAsync(Guid userPk, bool active) =>
        Task.FromResult(Returning.Try(() =>
        {
            var user = _ctx.Users.FindById(userPk);
            if (user is null)
                return Returning.Unfinished("Usuario no encontrado", UnfinishedInfo.NotifyType.Warning);

            user.IsActive = active;
            _ctx.Users.Update(user);
            return Returning.Success();
        }).SaveLog());

    public Task<Returning> ResetPasswordAsync(Guid userPk, string newPassword) =>
        Task.FromResult(Returning.Try(() =>
        {
            var user = _ctx.Users.FindById(userPk);
            if (user is null)
                return Returning.Unfinished("Usuario no encontrado", UnfinishedInfo.NotifyType.Warning);

            user.PasswordHash = _hasher.HashPassword(new DemoUser(), newPassword);
            _ctx.Users.Update(user);
            return Returning.Success();
        }).SaveLog());
}
