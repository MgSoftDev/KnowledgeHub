using MgSoftDev.KnowledgeHub.Blazor.Helpers;
using MgSoftDev.KnowledgeHub.Contracts;
using MgSoftDev.KnowledgeHub.Security;
using MgSoftDev.PrismPlus.Returning.Commands;
using MgSoftDev.ReturningCore;
using Microsoft.AspNetCore.Components;
using Radzen;

namespace MgSoftDev.KnowledgeHub.Blazor.Components.Pages;

public partial class Permissions : ComponentBase
{
    [Parameter] public Guid PagePk { get; set; }

    [Inject] private IKnowledgeHubPageService DocService { get; set; } = null!;
    [Inject] private IKnowledgeHubUserContext User { get; set; } = null!;
    [Inject] private KnowledgeHubOptions CoreOptions { get; set; } = null!;
    [Inject] private NavigationManager Nav { get; set; } = null!;
    [Inject] private NotificationService Notify { get; set; } = null!;

    protected List<PermissionInfo> Catalog { get; private set; } = new();
    protected IEnumerable<string> SelectedPermissions { get; set; } = new List<string>();
    protected bool IsPublic { get; set; }
    protected bool Loading { get; private set; } = true;
    public bool Wait { get; private set; }

    protected bool CanManage => User.CanManagePermissions(CoreOptions);

    protected override async Task OnParametersSetAsync()
    {
        if (!CanManage)
        {
            Loading = false;
            return;
        }

        Loading = true;

        var catalog = await User.GetPermissionCatalogAsync();
        Catalog = catalog.OkNotNull ? catalog.Value : new List<PermissionInfo>();

        var permissions = await DocService.GetPermissionsAsync(PagePk);
        if (permissions.OkNotNull)
        {
            IsPublic = permissions.Value.IsPublic;
            SelectedPermissions = permissions.Value.Permissions;
        }

        Loading = false;
    }

    public AsyncReturningCommand SaveCommand =>
        field ??= new AsyncReturningCommand(async () =>
        {
            var result = await DocService.SetPermissionsAsync(PagePk, IsPublic, SelectedPermissions.ToList());
            if (!result.Ok) return result;

            Notify.ShowSuccess("Visibilidad actualizada");
            return Returning.Success();
        }, () => !Wait)
        .StartAction(() => Wait = true)
        .EndAction(r =>
        {
            Wait = false;
            r.SendNotifyIfNotOk(Notify, "Error al guardar la visibilidad");
            StateHasChanged();
        });
}
