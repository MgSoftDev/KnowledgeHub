using MgSoftDev.KnowledgeHub.Blazor.EditorTools;
using MgSoftDev.KnowledgeHub.Blazor.Helpers;
using MgSoftDev.KnowledgeHub.Contracts;
using MgSoftDev.KnowledgeHub.Dtos;
using MgSoftDev.KnowledgeHub.Security;
using MgSoftDev.PrismPlus.Returning.Commands;
using MgSoftDev.ReturningCore;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using Radzen;

namespace MgSoftDev.KnowledgeHub.Blazor.Components.Embedded;

/// <summary>
/// Editor of a page (Radzen HTML editor + injectable custom tools). Embeddable anywhere;
/// supply the callbacks to stay inside your own screen after publishing/discarding, or omit
/// them to fall back to URL navigation over the built-in /kh routes.
/// </summary>
public partial class KnowledgeHubPageEditor : ComponentBase, IAsyncDisposable
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
    [Inject] private KnowledgeHubUiState UiState { get; set; } = null!;
    [Inject] private IJSRuntime JS { get; set; } = null!;

    private IJSObjectReference? _module;
    private ElementReference _editorHost;

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

            // Saving a broken template is allowed on purpose — you may be mid-loop — but you get
            // told now, with the line, instead of finding out when publishing is refused.
            await WarnAboutTemplateAsync();
            return Returning.Success();
        }, () => !Wait && SelectItem is not null)
        .StartAction(() => Wait = true)
        .EndAction(r =>
        {
            Wait = false;
            r.SendNotifyIfNotOk(Notify, "Error al guardar el borrador");
            StateHasChanged();
        });

    /// <summary>
    /// Shows the template's syntax errors after a save, if the page uses templates. Silent when it
    /// does not, or when no engine is registered — the service answers "no errors" then.
    /// </summary>
    private async Task WarnAboutTemplateAsync()
    {
        if (SelectItem is not { UsesTemplates: true }) return;

        var errors = await DocService.ValidateTemplateAsync(SelectItem.ContentHtml ?? string.Empty);
        if (errors is not { OkNotNull: true, Value.Count: > 0 }) return;

        Notify.Notify(new NotificationMessage
        {
            Severity = NotificationSeverity.Warning,
            Summary = "La plantilla tiene errores",
            Detail = string.Join("; ", errors.Value.Select(e => e.ToString())) +
                     ". Se guardó el borrador, pero no podrás publicarlo así.",
            Duration = 8000
        });
    }

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

            // Publicar mueve el título de la página al de la versión publicada, así que el árbol
            // puede haber quedado desactualizado.
            UiState.NotifyPageTreeChanged();

            // El cuerpo async de AsyncReturningCommand NO resume en el Dispatcher de Blazor, y
            // el callback provoca un render en el anfitrión (StateHasChanged) → hay que
            // marshalarlo con InvokeAsync. Si ya estamos en el Dispatcher se ejecuta inline.
            var publishedPk = SelectItem.PagePk;
            await InvokeAsync(async () =>
            {
                if (OnPublished.HasDelegate) await OnPublished.InvokeAsync(publishedPk);
                else Nav.NavigateTo(KnowledgeHubRoutes.Page(publishedPk));
            });
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

        // RadzenHtmlEditorCustomTool does NOT save the selection before raising Execute (the
        // built-in image tool does it itself). Without this, any tool that opens a dialog loses
        // the selection — the dialog takes the focus — and cannot replace what the user picked.
        await args.Editor.SaveSelectionAsync();

        var context = new EditorToolContext
        {
            Dialog = Dialog,
            Services = Services,
            User = User,
            Editor = args.Editor,
            GetHtml = () => SelectItem?.ContentHtml ?? string.Empty,
            GetHtmlAsync = GetEditorHtmlAsync,
            ReplaceAllAsync = ReplaceEditorHtmlAsync
        };
        var html = await tool.ExecuteAsync(context);

        if (html is not null)
            await args.Editor.ExecuteCommandAsync(HtmlEditorCommands.InsertHtml, html);
    }

    /// <summary>
    /// The document as it stands now. In the code view the bound value can be BEHIND what the
    /// author has typed —Radzen propagates the textarea on blur, and the toolbar buttons keep the
    /// focus on purpose— so the textarea is asked first. Everywhere else, and whenever JS is
    /// unavailable, this is exactly the old behaviour.
    /// </summary>
    private async Task<string> GetEditorHtmlAsync() =>
        await InvokeModuleAsync<string?>(m => m.InvokeAsync<string?>("readEditorSource", _editorHost))
        ?? SelectItem?.ContentHtml ?? string.Empty;

    /// <summary>
    /// Swaps the whole document (used by document-wide tools such as cleanup and formatting).
    ///
    /// In the code view it writes the textarea and lets Radzen's own change event carry the value
    /// back, instead of assigning the bound property: Radzen only copies that property into its
    /// internal field during OnAfterRender, which under Blazor Server lands after the batch is
    /// acknowledged — long enough for a re-render to repaint the textarea with the previous text.
    /// </summary>
    private async Task ReplaceEditorHtmlAsync(string html)
    {
        if (SelectItem is null) return;

        if (await InvokeModuleAsync<bool>(m => m.InvokeAsync<bool>("writeEditorSource", _editorHost, html)))
            return;

        SelectItem.ContentHtml = html;
        await InvokeAsync(StateHasChanged);
    }

    private async Task<T?> InvokeModuleAsync<T>(Func<IJSObjectReference, ValueTask<T>> call)
    {
        try
        {
            _module ??= await JS.InvokeAsync<IJSObjectReference>(
                "import", "./_content/MgSoftDev.KnowledgeHub.Blazor/knowledgehub.js");
            return await call(_module);
        }
        catch (JSDisconnectedException)
        {
            return default;
        }
        catch (JSException)
        {
            return default;
        }
        catch (InvalidOperationException)
        {
            // Prerendering, or the circuit went away mid-call: fall back to the bound value.
            return default;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_module is null) return;

        try
        {
            await _module.DisposeAsync();
        }
        catch (JSDisconnectedException)
        {
        }
        catch (JSException)
        {
        }

        _module = null;
    }

    /// <summary>
    /// Paste hook, wired ONLY when a sanitizer is registered — see <see cref="OnInitialized"/>.
    /// </summary>
    protected EventCallback<HtmlEditorPasteEventArgs> PasteCallback { get; private set; }

    protected override void OnInitialized()
    {
        // Binding Paste is NOT free: Radzen only raises the event when the callback has a
        // delegate, and doing so switches pasting from the browser's native path to a JS→.NET
        // round trip carrying the WHOLE pasted document. In Blazor Server that hits the SignalR
        // MaximumReceiveMessageSize limit (32 KB by default) and the paste is dropped in silence.
        // So only pay that price when there is actually something to clean.
        if (Services.GetService<IKnowledgeHubHtmlSanitizer>() is not null)
            PasteCallback = EventCallback.Factory.Create<HtmlEditorPasteEventArgs>(this, OnEditorPaste);
    }

    /// <summary>Cleans content pasted into the editor (Word markup, scripts…).</summary>
    private void OnEditorPaste(HtmlEditorPasteEventArgs args)
    {
        var sanitizer = Services.GetService<IKnowledgeHubHtmlSanitizer>();
        if (sanitizer is null || string.IsNullOrEmpty(args.Html)) return;

        args.Html = sanitizer.Sanitize(args.Html, HtmlSanitizeContext.Paste, UiState.CleanupLevel);
    }
}
