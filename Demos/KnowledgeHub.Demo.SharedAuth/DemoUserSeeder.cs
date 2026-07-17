using MgSoftDev.ReturningCore;
using MgSoftDev.ReturningCore.Exceptions;
using MgSoftDev.ReturningCore.Helper;
using Microsoft.AspNetCore.Identity;

namespace KnowledgeHub.Demo.SharedAuth;

/// <summary>Seeds the demo host's roles and users (all with password Demo123!).</summary>
public sealed class DemoUserSeeder
{
    private readonly DemoAuthContext _ctx;
    private readonly IPasswordHasher<DemoUser> _hasher;

    public DemoUserSeeder(DemoAuthContext ctx, IPasswordHasher<DemoUser> hasher)
    {
        _ctx = ctx;
        _hasher = hasher;
    }

    public Task<Returning> SeedIfEmptyAsync() =>
        Task.FromResult(Returning.Try(() =>
        {
            if (_ctx.Users.Query().ToList().Count > 0)
                return Returning.Success();

            var password = _hasher.HashPassword(new DemoUser(), "Demo123!");

            var admin = NewRole("Admin", "Acceso total: ve y edita todo");
            var editor = NewRole("Editor", "Puede crear, editar y publicar");
            var prod = NewRole("Produccion", "Lectura de producción");
            var ofi = NewRole("Oficinas", "Lectura de oficinas");
            var viewer = NewRole("Viewer", "Solo lectura pública");
            _ctx.Roles.InsertBulk(new[] { admin, editor, prod, ofi, viewer });

            _ctx.Users.InsertBulk(new[]
            {
                NewUser("admin", "Administrador del Sistema", password, admin.Pk),
                NewUser("editor1", "Editor de Contenido", password, editor.Pk),
                NewUser("prod1", "Usuario de Producción", password, prod.Pk),
                NewUser("ofi1", "Usuario de Oficinas", password, ofi.Pk)
            });

            return Returning.Success();
        }).SaveLog());

    private static DemoRole NewRole(string name, string description) =>
        new() { Pk = Guid.CreateVersion7(), Name = name, Description = description };

    private static DemoUser NewUser(string userName, string fullName, string passwordHash, Guid rolePk) =>
        new()
        {
            Pk = Guid.CreateVersion7(),
            UserName = userName,
            FullName = fullName,
            PasswordHash = passwordHash,
            RoleIds = new List<Guid> { rolePk }
        };
}
