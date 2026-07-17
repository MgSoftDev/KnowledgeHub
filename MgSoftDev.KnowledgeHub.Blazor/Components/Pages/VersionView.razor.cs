using MgSoftDev.KnowledgeHub.Contracts;
using MgSoftDev.KnowledgeHub.Dtos;
using Microsoft.AspNetCore.Components;

namespace MgSoftDev.KnowledgeHub.Blazor.Components.Pages;

public partial class VersionView : ComponentBase
{
    [Parameter] public Guid VersionPk { get; set; }

    [Inject] private IKnowledgeHubPageService DocService { get; set; } = null!;
    [Inject] private IKnowledgeHubHtmlImageRewriter Rewriter { get; set; } = null!;
    [Inject] private NavigationManager Nav { get; set; } = null!;

    protected PageReadDto? Page { get; private set; }
    protected string RenderedHtml { get; private set; } = string.Empty;
    protected string? ErrorMessage { get; private set; }
    protected bool Loading { get; private set; } = true;

    protected override async Task OnParametersSetAsync()
    {
        Loading = true;
        ErrorMessage = null;
        Page = null;

        var result = await DocService.GetVersionContentAsync(VersionPk);
        if (!result.OkNotNull)
        {
            ErrorMessage = result.UnfinishedInfo?.Title ?? "No se pudo cargar la versión.";
            Loading = false;
            return;
        }

        Page = result.Value;
        var rewrite = await Rewriter.PrepareForDisplayAsync(Page.ContentHtml);
        RenderedHtml = rewrite.OkNotNull ? rewrite.Value.Html : Page.ContentHtml;
        Loading = false;
    }
}
