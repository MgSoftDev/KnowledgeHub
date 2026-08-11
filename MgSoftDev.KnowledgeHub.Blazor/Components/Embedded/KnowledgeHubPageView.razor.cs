using System.Diagnostics;
using MgSoftDev.KnowledgeHub.Contracts;
using MgSoftDev.KnowledgeHub.Dtos;
using MgSoftDev.KnowledgeHub.Security;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using Radzen;
using Radzen.Blazor;

namespace MgSoftDev.KnowledgeHub.Blazor.Components.Embedded;

/// <summary>
/// Reads and renders the published version of a page (images resolved through the rewriter).
/// Embeddable anywhere; supply the action callbacks to keep the user inside your own screen,
/// or omit them to fall back to URL navigation over the built-in /kh routes.
/// </summary>
public partial class KnowledgeHubPageView : ComponentBase
{
    [Parameter] public Guid PagePk { get; set; }

    /// <summary>Show the meta/actions bar (version + Edit/Permissions/Manage/History). Default true.</summary>
    [Parameter] public bool ShowActions { get; set; } = true;

    /// <summary>Without a handler, navigates to /kh/edit/{pk}.</summary>
    [Parameter] public EventCallback<Guid> OnEditRequested { get; set; }

    /// <summary>Without a handler, navigates to /kh/history/{pk}.</summary>
    [Parameter] public EventCallback<Guid> OnHistoryRequested { get; set; }

    /// <summary>Without a handler, navigates to /kh/permissions/{pk}.</summary>
    [Parameter] public EventCallback<Guid> OnPermissionsRequested { get; set; }

    /// <summary>Without a handler, navigates to /kh/manage/{pk}.</summary>
    [Parameter] public EventCallback<Guid> OnManageRequested { get; set; }

    [Inject] private IKnowledgeHubPageService DocService { get; set; } = null!;
    [Inject] private IKnowledgeHubHtmlImageRewriter Rewriter { get; set; } = null!;
    [Inject] private IKnowledgeHubDiagnostics Diagnostics { get; set; } = null!;
    [Inject] private IKnowledgeHubUserContext User { get; set; } = null!;
    [Inject] private NavigationManager Nav { get; set; } = null!;
    [Inject] private IServiceProvider Services { get; set; } = null!;
    [Inject] private KnowledgeHubOptions CoreOptions { get; set; } = null!;
    [Inject] private NotificationService Notify { get; set; } = null!;
    [Inject] private IJSRuntime JS { get; set; } = null!;

    protected PageReadDto? Page { get; private set; }
    protected string RenderedHtml { get; private set; } = string.Empty;
    protected string? ErrorMessage { get; private set; }
    protected bool Loading { get; private set; } = true;
    protected bool Exporting { get; private set; }

    /// <summary>
    /// The export button shows only when the user may export AND something can actually produce a
    /// file — same "hide what cannot work" rule as the editor's cleanup buttons.
    /// </summary>
    protected bool CanExport =>
        User.CanExport(CoreOptions) && Services.GetService<IKnowledgeHubPdfExportService>() is not null;

    // Fires whenever PagePk changes, both as a route parameter and as a component parameter.
    protected override async Task OnParametersSetAsync() => await LoadAsync();

    private async Task LoadAsync()
    {
        Loading = true;
        ErrorMessage = null;
        Page = null;

        var totalStopwatch = Stopwatch.StartNew();

        // Query 1: the published version's HTML.
        var htmlStopwatch = Stopwatch.StartNew();
        var result = await DocService.GetPageForReadAsync(PagePk);
        htmlStopwatch.Stop();

        if (!result.OkNotNull)
        {
            ErrorMessage = result.UnfinishedInfo?.Title ?? "No se pudo cargar la página.";
            Loading = false;
            return;
        }

        Page = result.Value;

        var snapshot = new DiagnosticsSnapshot
        {
            PageTitle = Page.Title,
            HtmlQueryMs = htmlStopwatch.Elapsed.TotalMilliseconds
        };

        // Queries 2 & 3 + cache: rewrite docimg:// to display URLs.
        var rewriteResult = await Rewriter.PrepareForDisplayAsync(Page.ContentHtml);
        if (rewriteResult.OkNotNull)
        {
            var rewrite = rewriteResult.Value;
            RenderedHtml = rewrite.Html;
            snapshot.HashesQueryMs = rewrite.HashesQueryMs;
            snapshot.BlobsQueryMs = rewrite.BlobsQueryMs;
            snapshot.ImageCount = rewrite.ImageCount;
            snapshot.CacheHits = rewrite.CacheHits;
            snapshot.CacheMisses = rewrite.CacheMisses;
            snapshot.BytesFromStore = rewrite.BytesFromStore;
        }
        else
        {
            RenderedHtml = Page.ContentHtml;
        }

        totalStopwatch.Stop();
        snapshot.TotalMs = totalStopwatch.Elapsed.TotalMilliseconds;
        Diagnostics.Record(snapshot);

        Loading = false;
    }

    private async Task GoEdit()
    {
        if (OnEditRequested.HasDelegate) await OnEditRequested.InvokeAsync(PagePk);
        else Nav.NavigateTo(KnowledgeHubRoutes.Edit(PagePk));
    }

    private async Task GoHistory()
    {
        if (OnHistoryRequested.HasDelegate) await OnHistoryRequested.InvokeAsync(PagePk);
        else Nav.NavigateTo(KnowledgeHubRoutes.History(PagePk));
    }

    private async Task GoPermissions()
    {
        if (OnPermissionsRequested.HasDelegate) await OnPermissionsRequested.InvokeAsync(PagePk);
        else Nav.NavigateTo(KnowledgeHubRoutes.Permissions(PagePk));
    }

    private async Task GoManage()
    {
        if (OnManageRequested.HasDelegate) await OnManageRequested.InvokeAsync(PagePk);
        else Nav.NavigateTo(KnowledgeHubRoutes.Manage(PagePk));
    }

    /// <summary>Main button (null item) exports this page only; the menu offers the branch.</summary>
    protected Task OnExportClick(RadzenSplitButtonItem? item) =>
        ExportPdfAsync(item?.Value as string == "branch");

    /// <param name="includeDescendants">Export the whole branch instead of just this page.</param>
    protected async Task ExportPdfAsync(bool includeDescendants)
    {
        var exporter = Services.GetService<IKnowledgeHubPdfExportService>();
        if (exporter is null || Exporting) return;

        Exporting = true;
        StateHasChanged();
        try
        {
            var result = await exporter.ExportAsync(PagePk, includeDescendants);
            if (!result.OkNotNull)
            {
                Notify.Notify(new NotificationMessage
                {
                    Severity = NotificationSeverity.Warning,
                    Summary = "No se pudo exportar",
                    Detail = result.UnfinishedInfo?.Title ?? "Inténtalo de nuevo."
                });
                return;
            }

            await SaveAsync(result.Value);
        }
        finally
        {
            Exporting = false;
            StateHasChanged();
        }
    }

    /// <summary>
    /// Hands the bytes to the browser as a download.
    ///
    /// Streamed rather than base64-encoded: under Blazor Server this crosses SignalR, and base64
    /// would add a third to the size and hit the message limit — the wall the paste path already
    /// ran into.
    /// </summary>
    private async Task SaveAsync(PdfFileDto file)
    {
        using var stream = new MemoryStream(file.Content);
        using var reference = new DotNetStreamReference(stream);

        var module = await JS.InvokeAsync<IJSObjectReference>(
            "import", "./_content/MgSoftDev.KnowledgeHub.Blazor/knowledgehub.js");
        await using (module)
        {
            await module.InvokeVoidAsync("downloadFileFromStream", file.FileName, file.ContentType, reference);
        }
    }
}
