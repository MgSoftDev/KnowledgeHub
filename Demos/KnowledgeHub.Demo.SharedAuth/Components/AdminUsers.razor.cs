using MgSoftDev.KnowledgeHub.Blazor.Helpers;
using MgSoftDev.KnowledgeHub.Contracts;
using MgSoftDev.PrismPlus.Returning.Commands;
using MgSoftDev.ReturningCore;
using Microsoft.AspNetCore.Components;
using Radzen;

namespace KnowledgeHub.Demo.SharedAuth.Components;

public partial class AdminUsers : ComponentBase
{
    [Inject] private DemoAdminService AdminService { get; set; } = null!;
    [Inject] private IKnowledgeHubUserContext User { get; set; } = null!;
    [Inject] private NotificationService Notify { get; set; } = null!;

    protected List<DemoUserAdminDto> Users { get; private set; } = new();
    protected List<DemoRoleDto> Roles { get; private set; } = new();
    public bool Wait { get; private set; }

    // New-user form.
    protected string NewUserName { get; set; } = string.Empty;
    protected string NewFullName { get; set; } = string.Empty;
    protected string NewPassword { get; set; } = string.Empty;
    protected IEnumerable<Guid> NewUserRoleIds { get; set; } = new List<Guid>();

    // New-role form.
    protected string NewRoleName { get; set; } = string.Empty;
    protected string NewRoleDescription { get; set; } = string.Empty;

    // Inline role editor.
    protected DemoUserAdminDto? EditingUser { get; set; }
    protected IEnumerable<Guid> EditingUserRoleIds { get; set; } = new List<Guid>();

    protected override async Task OnInitializedAsync() => await LoadAsync();

    private async Task LoadAsync()
    {
        var users = await AdminService.GetUsersAsync();
        Users = users.OkNotNull ? users.Value : new List<DemoUserAdminDto>();

        var roles = await AdminService.GetRolesAsync();
        Roles = roles.OkNotNull ? roles.Value : new List<DemoRoleDto>();
    }

    public AsyncReturningCommand CreateUserCommand =>
        field ??= new AsyncReturningCommand(async () =>
        {
            var result = await AdminService.CreateUserAsync(NewUserName, NewFullName, NewPassword, NewUserRoleIds.ToList());
            if (!result.Ok) return result;

            Notify.ShowSuccess("Usuario creado");
            NewUserName = NewFullName = NewPassword = string.Empty;
            NewUserRoleIds = new List<Guid>();
            await LoadAsync();
            return Returning.Success();
        }, () => !Wait)
        .StartAction(() => Wait = true)
        .EndAction(r => { Wait = false; r.SendNotifyIfNotOk(Notify, "Error al crear el usuario"); StateHasChanged(); });

    public AsyncReturningCommand CreateRoleCommand =>
        field ??= new AsyncReturningCommand(async () =>
        {
            var result = await AdminService.CreateRoleAsync(NewRoleName, NewRoleDescription);
            if (!result.Ok) return result;

            Notify.ShowSuccess("Rol creado");
            NewRoleName = NewRoleDescription = string.Empty;
            await LoadAsync();
            return Returning.Success();
        }, () => !Wait)
        .StartAction(() => Wait = true)
        .EndAction(r => { Wait = false; r.SendNotifyIfNotOk(Notify, "Error al crear el rol"); StateHasChanged(); });

    public AsyncReturningCommand SaveUserRolesCommand =>
        field ??= new AsyncReturningCommand(async () =>
        {
            if (EditingUser is null)
                return Returning.Unfinished("No hay usuario seleccionado", UnfinishedInfo.NotifyType.Warning);

            var result = await AdminService.UpdateUserRolesAsync(EditingUser.Pk, EditingUserRoleIds.ToList());
            if (!result.Ok) return result;

            Notify.ShowSuccess("Roles actualizados");
            EditingUser = null;
            await LoadAsync();
            return Returning.Success();
        }, () => !Wait)
        .StartAction(() => Wait = true)
        .EndAction(r => { Wait = false; r.SendNotifyIfNotOk(Notify, "Error al actualizar los roles"); StateHasChanged(); });

    private void BeginEditRoles(DemoUserAdminDto user)
    {
        EditingUser = user;
        EditingUserRoleIds = new List<Guid>(user.RoleIds);
    }

    private async Task ToggleActiveAsync(DemoUserAdminDto user)
    {
        Wait = true;
        StateHasChanged();
        var result = await AdminService.SetUserActiveAsync(user.Pk, !user.IsActive);
        Wait = false;
        if (result.Ok) { await LoadAsync(); }
        else result.SendNotifyIfNotOk(Notify, "Error al cambiar el estado");
        StateHasChanged();
    }

    private async Task ResetPasswordAsync(DemoUserAdminDto user)
    {
        Wait = true;
        StateHasChanged();
        var result = await AdminService.ResetPasswordAsync(user.Pk, "Demo123!");
        Wait = false;
        if (result.Ok) Notify.ShowInfo($"Contraseña de {user.UserName} restablecida a Demo123!");
        else result.SendNotifyIfNotOk(Notify, "Error al restablecer la contraseña");
        StateHasChanged();
    }
}
