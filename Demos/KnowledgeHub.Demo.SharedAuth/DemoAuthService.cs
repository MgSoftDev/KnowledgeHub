using MgSoftDev.ReturningCore;
using MgSoftDev.ReturningCore.Exceptions;
using MgSoftDev.ReturningCore.Helper;
using Microsoft.AspNetCore.Identity;

namespace KnowledgeHub.Demo.SharedAuth;

/// <summary>Authenticates against the demo host's users with <see cref="IPasswordHasher{TUser}"/>.</summary>
public sealed class DemoAuthService
{
    private readonly DemoAuthContext _ctx;
    private readonly IPasswordHasher<DemoUser> _hasher;

    public DemoAuthService(DemoAuthContext ctx, IPasswordHasher<DemoUser> hasher)
    {
        _ctx = ctx;
        _hasher = hasher;
    }

    public Task<Returning<DemoUserDto>> LoginAsync(string userName, string password) =>
        Task.FromResult(Returning<DemoUserDto>.Try(() =>
        {
            if (string.IsNullOrWhiteSpace(userName) || string.IsNullOrWhiteSpace(password))
                return Returning.Unfinished("Ingresa usuario y contraseña", UnfinishedInfo.NotifyType.Warning);

            var user = _ctx.Users.Query().Where(u => u.UserName == userName).FirstOrDefault();
            if (user is null || !user.IsActive)
                return Returning.Unfinished("Usuario o contraseña incorrectos", UnfinishedInfo.NotifyType.Warning);

            var verify = _hasher.VerifyHashedPassword(new DemoUser(), user.PasswordHash, password);
            if (verify == PasswordVerificationResult.Failed)
                return Returning.Unfinished("Usuario o contraseña incorrectos", UnfinishedInfo.NotifyType.Warning);

            return ToDto(user);
        }).SaveLog());

    /// <summary>Loads a user WITHOUT password verification. Dev auto-login only.</summary>
    public Task<Returning<DemoUserDto>> GetUserAsync(string userName) =>
        Task.FromResult(Returning<DemoUserDto>.Try(() =>
        {
            var user = _ctx.Users.Query().Where(u => u.UserName == userName).FirstOrDefault();
            if (user is null || !user.IsActive)
                return Returning.Unfinished("Usuario no encontrado", UnfinishedInfo.NotifyType.Warning);
            return ToDto(user);
        }).SaveLog());

    private DemoUserDto ToDto(DemoUser user)
    {
        var roleNames = _ctx.Roles.Query().ToList()
            .Where(r => r.IsActive && user.RoleIds.Contains(r.Pk))
            .Select(r => r.Name)
            .ToList();
        return new DemoUserDto(user.Pk, user.UserName, user.FullName, roleNames);
    }
}
