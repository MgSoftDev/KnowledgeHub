using MgSoftDev.KnowledgeHub.Blazor.Helpers;
using MgSoftDev.KnowledgeHub.Contracts;
using MgSoftDev.KnowledgeHub.Dtos;
using MgSoftDev.KnowledgeHub.Security;
using Microsoft.AspNetCore.Components;
using Radzen;

namespace MgSoftDev.KnowledgeHub.Blazor.Components.Embedded;

/// <summary>
/// Version history of a page (view / restore). Embeddable; supply the callbacks to handle
/// navigation yourself, or omit them to fall back to the built-in /kh routes.
/// </summary>
public partial class KnowledgeHubPageHistory : ComponentBase
{
    [Parameter] public Guid PagePk { get; set; }

    /// <summary>Without a handler, navigates to /kh/version/{versionPk}.</summary>
    [Parameter] public EventCallback<Guid> OnVersionRequested { get; set; }

    /// <summary>Without a handler, navigates to /kh/page/{pk}.</summary>
    [Parameter] public EventCallback<Guid> OnBackRequested { get; set; }

    [Inject] private IKnowledgeHubPageService DocService { get; set; } = null!;
    [Inject] private IKnowledgeHubUserContext User { get; set; } = null!;
    [Inject] private NavigationManager Nav { get; set; } = null!;
    [Inject] private NotificationService Notify { get; set; } = null!;

    protected List<VersionListItemDto> Versions { get; private set; } = new();
    protected bool Loading { get; private set; } = true;
    public bool Wait { get; private set; }

    protected override async Task OnParametersSetAsync() => await LoadAsync();

    private async Task LoadAsync()
    {
        Loading = true;
        var result = await DocService.GetVersionsAsync(PagePk);
        Versions = result.OkNotNull ? result.Value : new List<VersionListItemDto>();
        Loading = false;
    }

    private async Task GoVersion(Guid versionPk)
    {
        if (OnVersionRequested.HasDelegate) await OnVersionRequested.InvokeAsync(versionPk);
        else Nav.NavigateTo(KnowledgeHubRoutes.Version(versionPk));
    }

    private async Task GoBack()
    {
        if (OnBackRequested.HasDelegate) await OnBackRequested.InvokeAsync(PagePk);
        else Nav.NavigateTo(KnowledgeHubRoutes.Page(PagePk));
    }

    private async Task RestoreAsync(Guid versionPk)
    {
        Wait = true;
        StateHasChanged();

        var result = await DocService.RestoreVersionAsync(versionPk);
        Wait = false;

        if (result.Ok)
        {
            Notify.ShowSuccess("Versión restaurada como nuevo borrador");
            await LoadAsync();
        }
        else
        {
            result.SendNotifyIfNotOk(Notify, "Error al restaurar la versión");
        }
        StateHasChanged();
    }
}
