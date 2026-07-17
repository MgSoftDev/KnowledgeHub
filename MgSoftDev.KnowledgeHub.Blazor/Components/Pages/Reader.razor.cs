using System.Diagnostics;
using MgSoftDev.KnowledgeHub.Contracts;
using MgSoftDev.KnowledgeHub.Dtos;
using MgSoftDev.KnowledgeHub.Security;
using Microsoft.AspNetCore.Components;

namespace MgSoftDev.KnowledgeHub.Blazor.Components.Pages;

public partial class Reader : ComponentBase
{
    [Parameter] public Guid PagePk { get; set; }

    [Inject] private IKnowledgeHubPageService DocService { get; set; } = null!;
    [Inject] private IKnowledgeHubHtmlImageRewriter Rewriter { get; set; } = null!;
    [Inject] private IKnowledgeHubDiagnostics Diagnostics { get; set; } = null!;
    [Inject] private IKnowledgeHubUserContext User { get; set; } = null!;
    [Inject] private NavigationManager Nav { get; set; } = null!;

    protected PageReadDto? Page { get; private set; }
    protected string RenderedHtml { get; private set; } = string.Empty;
    protected string? ErrorMessage { get; private set; }
    protected bool Loading { get; private set; } = true;

    // Fires whenever the route parameter changes, i.e. when navigating between pages.
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

    private void GoEdit() => Nav.NavigateTo(KnowledgeHubRoutes.Edit(PagePk));
    private void GoHistory() => Nav.NavigateTo(KnowledgeHubRoutes.History(PagePk));
    private void GoPermissions() => Nav.NavigateTo(KnowledgeHubRoutes.Permissions(PagePk));
    private void GoManage() => Nav.NavigateTo(KnowledgeHubRoutes.Manage(PagePk));
}
