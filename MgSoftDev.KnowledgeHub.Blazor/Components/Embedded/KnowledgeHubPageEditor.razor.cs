using MgSoftDev.KnowledgeHub.Blazor.EditorTools;
using MgSoftDev.KnowledgeHub.Blazor.Helpers;
using MgSoftDev.KnowledgeHub.Contracts;
using MgSoftDev.KnowledgeHub.Dtos;
using MgSoftDev.KnowledgeHub.Security;
using MgSoftDev.PrismPlus.Returning.Commands;
using MgSoftDev.ReturningCore;
using Microsoft.AspNetCore.Components;
using Radzen;

namespace MgSoftDev.KnowledgeHub.Blazor.Components.Embedded;

/// <summary>
/// Editor of a page (Radzen HTML editor + injectable custom tools). Embeddable anywhere;
/// supply the callbacks to stay inside your own screen after publishing/discarding, or omit
/// them to fall back to URL navigation over the built-in /kh routes.
/// </summary>
public partial class KnowledgeHubPageEditor : ComponentBase
{
    [Parameter] public Guid PagePk { get; set; }

    /// <summary>Raised after publishing. Without a handler, navigates to /kh/page/{pk}.</summary>
    [Parameter] public EventCallback<Guid> OnPublished { get; set; }

    /// <summary>Raised on Discard. Without a handler, navigates to /kh/page/{pk}.</summary>
    [Parameter] public EventCallback<Guid> OnDiscarded { get; set; }

    [Inject] private IKnowledgeHubPageService DocService { get; set; } = null!;
    [Inject] private IKnowledgeHubHtmlImageRewriter Rewriter { get; set; } = null!;
    [Inject] private IKnowledgeHubUserContext User { get; set; } = null!;
    [Inject] private KnowledgeHubBlazorOptions Options { get; set; } = null!;
    [Inject] private IServiceProvider Services { get; set; } = null!;
    [Inject] private NavigationManager Nav { get; set; } = null!;
    [Inject] private NotificationService Notify { get; set; } = null!;
    [Inject] private DialogService Dialog { get; set; } = null!;

    protected PageEditDto? SelectItem { get; private set; }
    protected bool Loading { get; private set; } = true;
    public bool Wait { get; set; }

    protected override async Task OnParametersSetAsync()
    {
        if (!User.CanEdit())
        {
            Loading = false;
            return;
        }

        Loading = true;
        var result = await DocService.GetPageForEditAsync(PagePk);
        if (result.OkNotNull)
        {
            SelectItem = result.Value;
            // Rewrite docimg:// to display URLs so existing images render in the editor.
            // On save they are turned back into docimg:// references by the page service.
            var rewrite = await Rewriter.PrepareForDisplayAsync(SelectItem.ContentHtml);
            if (rewrite.OkNotNull) SelectItem.ContentHtml = rewrite.Value.Html;
        }
        else
        {
            SelectItem = null;
        }
        Loading = false;
    }

    public AsyncReturningCommand SaveDraftCommand =>
        field ??= new AsyncReturningCommand(async () =>
        {
            if (SelectItem is null)
                return Returning.Unfinished("No hay contenido para guardar", UnfinishedInfo.NotifyType.Warning);

            var result = await DocService.SaveDraftAsync(SelectItem);
            if (!result.Ok) return result;

            Notify.ShowSuccess("Borrador guardado");
            return Returning.Success();
        }, () => !Wait && SelectItem is not null)
        .StartAction(() => Wait = true)
        .EndAction(r =>
        {
            Wait = false;
            r.SendNotifyIfNotOk(Notify, "Error al guardar el borrador");
            StateHasChanged();
        });

    public AsyncReturningCommand PublishCommand =>
        field ??= new AsyncReturningCommand(async () =>
        {
            if (SelectItem is null)
                return Returning.Unfinished("No hay contenido para publicar", UnfinishedInfo.NotifyType.Warning);

            // Persist the current editor content as a new version, then publish it.
            var save = await DocService.SaveDraftAsync(SelectItem);
            if (!save.Ok) return save;

            var publish = await DocService.PublishAsync(SelectItem.PagePk, SelectItem.BaseVersionNumber);
            if (!publish.Ok) return publish;

            Notify.ShowSuccess("Página publicada");
            if (OnPublished.HasDelegate) await OnPublished.InvokeAsync(SelectItem.PagePk);
            else Nav.NavigateTo(KnowledgeHubRoutes.Page(SelectItem.PagePk));
            return Returning.Success();
        }, () => !Wait && SelectItem is not null)
        .StartAction(() => Wait = true)
        .EndAction(r =>
        {
            Wait = false;
            r.SendNotifyIfNotOk(Notify, "Error al publicar");
            StateHasChanged();
        });

    private async Task Discard()
    {
        if (OnDiscarded.HasDelegate) await OnDiscarded.InvokeAsync(PagePk);
        else Nav.NavigateTo(KnowledgeHubRoutes.Page(PagePk));
    }

    /// <summary>
    /// Dispatches the custom toolbar buttons to their registered tool (built-in callouts and
    /// host-provided tools alike) and inserts the produced HTML at the caret.
    /// </summary>
    private async Task OnEditorExecute(HtmlEditorExecuteEventArgs args)
    {
        var tool = Options.EditorTools.FirstOrDefault(t => t.CommandName == args.CommandName);
        if (tool is null) return;

        var context = new EditorToolContext { Dialog = Dialog, Services = Services, User = User };
        var html = await tool.ExecuteAsync(context);

        if (html is not null)
            await args.Editor.ExecuteCommandAsync(HtmlEditorCommands.InsertHtml, html);
    }
}
