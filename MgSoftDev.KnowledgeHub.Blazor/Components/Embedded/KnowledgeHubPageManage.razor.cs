using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using MgSoftDev.KnowledgeHub.Blazor.Helpers;
using MgSoftDev.KnowledgeHub.Contracts;
using MgSoftDev.KnowledgeHub.Dtos;
using MgSoftDev.KnowledgeHub.Enums;
using MgSoftDev.KnowledgeHub.Security;
using MgSoftDev.ReturningCore;
using Microsoft.AspNetCore.Components;
using Radzen;

namespace MgSoftDev.KnowledgeHub.Blazor.Components.Embedded;

/// <summary>
/// Structural management of a page (rename, move, reorder, create child, delete). Embeddable;
/// supply the callbacks to stay inside your own screen, or omit them to fall back to /kh routes.
/// </summary>
public partial class KnowledgeHubPageManage : ComponentBase
{
    [Parameter] public Guid PagePk { get; set; }

    /// <summary>Without a handler, navigates to /kh/page/{pk}.</summary>
    [Parameter] public EventCallback<Guid> OnBackRequested { get; set; }

    /// <summary>Raised with the new child pk. Without a handler, navigates to /kh/edit/{pk}.</summary>
    [Parameter] public EventCallback<Guid> OnChildCreated { get; set; }

    /// <summary>Raised after deleting the page. Without a handler, navigates to /kh.</summary>
    [Parameter] public EventCallback OnDeleted { get; set; }

    [Inject] private IKnowledgeHubPageService DocService { get; set; } = null!;
    [Inject] private IKnowledgeHubUserContext User { get; set; } = null!;
    [Inject] private NavigationManager Nav { get; set; } = null!;
    [Inject] private NotificationService Notify { get; set; } = null!;
    [Inject] private DialogService Dialog { get; set; } = null!;
    [Inject] private KnowledgeHubUiState UiState { get; set; } = null!;

    protected PageInfoDto? Info { get; private set; }
    protected List<PageInfoDto> ParentOptions { get; private set; } = new();
    protected Guid? SelectedParent { get; set; }
    protected string NewChildTitle { get; set; } = string.Empty;
    protected bool Loading { get; private set; } = true;
    public bool Wait { get; private set; }

    /// <summary>1-based position among siblings, and how many there are. 0 when unknown.</summary>
    protected int SiblingPosition { get; private set; }
    protected int SiblingCount { get; private set; }
    protected bool IsFirstSibling => SiblingCount == 0 || SiblingPosition <= 1;
    protected bool IsLastSibling => SiblingCount == 0 || SiblingPosition >= SiblingCount;

    protected override async Task OnParametersSetAsync()
    {
        if (!User.CanEdit()) { Loading = false; return; }

        Loading = true;
        var info = await DocService.GetPageInfoAsync(PagePk);
        Info = info.OkNotNull ? info.Value : null;
        SelectedParent = Info?.Fk_DocPageParent;

        // Flatten the visible tree as parent options, excluding this page itself.
        var tree = await DocService.GetTreeAsync();
        ParentOptions = tree.OkNotNull
            ? Flatten(tree.Value).Where(p => p.Pk != PagePk).ToList()
            : new List<PageInfoDto>();

        if (tree.OkNotNull) ComputePosition(tree.Value);

        Loading = false;
    }

    /// <summary>
    /// Position within the sibling list AS THE TREE SHOWS IT, which is what the up/down buttons
    /// act on. Note the tree is permission-filtered: an editor who cannot see every sibling would
    /// see a partial position.
    /// </summary>
    private void ComputePosition(IEnumerable<PageTreeNodeDto> roots)
    {
        var siblings = FindSiblings(roots, PagePk);
        SiblingCount = siblings.Count;
        SiblingPosition = siblings.FindIndex(n => n.Pk == PagePk) + 1;
    }

    private static List<PageTreeNodeDto> FindSiblings(IEnumerable<PageTreeNodeDto> nodes, Guid pagePk)
    {
        var list = nodes.ToList();
        if (list.Any(n => n.Pk == pagePk)) return list;

        foreach (var node in list)
        {
            var found = FindSiblings(node.Children, pagePk);
            if (found.Count > 0) return found;
        }
        return new List<PageTreeNodeDto>();
    }

    private static IEnumerable<PageInfoDto> Flatten(IEnumerable<PageTreeNodeDto> nodes)
    {
        foreach (var node in nodes)
        {
            yield return new PageInfoDto { Pk = node.Pk, Title = node.Title, Slug = node.Slug };
            foreach (var child in Flatten(node.Children))
                yield return child;
        }
    }

    private async Task RenameAsync()
    {
        if (Info is null) return;
        await Run(() => DocService.RenamePageAsync(PagePk, Info.Title), "Página renombrada", "Error al renombrar");
    }

    private async Task MoveAsync() =>
        await Run(() => DocService.MovePageAsync(PagePk, SelectedParent), "Página movida", "Error al mover");

    /// <summary>
    /// One step up/down. Saves immediately and reloads the position, so the buttons disable
    /// themselves at the ends; the tree refresh comes from <see cref="Run"/>.
    /// </summary>
    private async Task MoveOrderAsync(PageMoveDirection direction)
    {
        await Run(() => DocService.MovePageOrderAsync(PagePk, direction),
            direction == PageMoveDirection.Up ? "Página subida" : "Página bajada",
            "Error al reordenar");

        var tree = await DocService.GetTreeAsync();
        if (tree.OkNotNull) ComputePosition(tree.Value);
    }

    private async Task SaveIconAsync()
    {
        if (Info is null) return;
        await Run(() => DocService.SetPageIconAsync(PagePk, Info.Icon, Info.IconColor),
            "Icono actualizado", "Error al guardar el icono");
    }

    private async Task CreateChildAsync()
    {
        if (string.IsNullOrWhiteSpace(NewChildTitle)) return;

        Wait = true;
        StateHasChanged();
        var slug = Slugify(NewChildTitle);
        var result = await DocService.CreatePageAsync(PagePk, NewChildTitle.Trim(), slug);
        Wait = false;

        if (result.OkNotNull)
        {
            Notify.ShowSuccess("Subpágina creada");
            UiState.NotifyPageTreeChanged();
            if (OnChildCreated.HasDelegate) await OnChildCreated.InvokeAsync(result.Value);
            else Nav.NavigateTo(KnowledgeHubRoutes.Edit(result.Value));
        }
        else
        {
            result.SendNotifyIfNotOk(Notify, "Error al crear la subpágina");
            StateHasChanged();
        }
    }

    private async Task DeleteAsync()
    {
        var confirm = await Dialog.Confirm("¿Eliminar esta página y sus subpáginas?", "Confirmar eliminación",
            new ConfirmOptions { OkButtonText = "Eliminar", CancelButtonText = "Cancelar" });
        if (confirm != true) return;

        Wait = true;
        StateHasChanged();
        var result = await DocService.DeletePageAsync(PagePk);
        Wait = false;

        if (result.Ok)
        {
            Notify.ShowSuccess("Página eliminada");
            UiState.NotifyPageTreeChanged();
            if (OnDeleted.HasDelegate) await OnDeleted.InvokeAsync();
            else Nav.NavigateTo(KnowledgeHubRoutes.Home);
        }
        else
        {
            result.SendNotifyIfNotOk(Notify, "Error al eliminar");
            StateHasChanged();
        }
    }

    private async Task Run(Func<Task<Returning>> action, string success, string errorTitle)
    {
        Wait = true;
        StateHasChanged();
        var result = await action();
        Wait = false;
        if (result.Ok)
        {
            Notify.ShowSuccess(success);
            // Title, parent, order and icon are all shown by the tree: tell it to reload.
            UiState.NotifyPageTreeChanged();
        }
        else result.SendNotifyIfNotOk(Notify, errorTitle);
        StateHasChanged();
    }

    private static string Slugify(string text)
    {
        var normalized = text.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (var c in normalized)
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                sb.Append(c);

        var slug = Regex.Replace(sb.ToString().Normalize(NormalizationForm.FormC), "[^a-z0-9]+", "-").Trim('-');
        return string.IsNullOrEmpty(slug) ? Guid.NewGuid().ToString("n")[..8] : slug;
    }

    private async Task GoBack()
    {
        if (OnBackRequested.HasDelegate) await OnBackRequested.InvokeAsync(PagePk);
        else Nav.NavigateTo(KnowledgeHubRoutes.Page(PagePk));
    }
}
