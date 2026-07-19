using MgSoftDev.KnowledgeHub.Blazor.Helpers;
using MgSoftDev.KnowledgeHub.Contracts;
using MgSoftDev.KnowledgeHub.Security;
using MgSoftDev.PrismPlus.Returning.Commands;
using MgSoftDev.ReturningCore;
using Microsoft.AspNetCore.Components;
using Radzen;

namespace MgSoftDev.KnowledgeHub.Blazor.Components.Embedded;

/// <summary>
/// Per-page visibility editor (public flag + host permission names). Embeddable; supply the
/// callback to handle "back" yourself, or omit it to fall back to /kh/page/{pk}.
/// </summary>
public partial class KnowledgeHubPagePermissions : ComponentBase
{
    [Parameter] public Guid PagePk { get; set; }

    /// <summary>Without a handler, navigates to /kh/page/{pk}.</summary>
    [Parameter] public EventCallback<Guid> OnBackRequested { get; set; }

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

    private async Task GoBack()
    {
        if (OnBackRequested.HasDelegate) await OnBackRequested.InvokeAsync(PagePk);
        else Nav.NavigateTo(KnowledgeHubRoutes.Page(PagePk));
    }
}
