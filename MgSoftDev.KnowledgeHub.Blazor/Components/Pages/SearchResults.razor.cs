using MgSoftDev.KnowledgeHub.Contracts;
using MgSoftDev.KnowledgeHub.Dtos;
using Microsoft.AspNetCore.Components;

namespace MgSoftDev.KnowledgeHub.Blazor.Components.Pages;

public partial class SearchResults : ComponentBase
{
    [Inject] private IKnowledgeHubPageService DocService { get; set; } = null!;

    [SupplyParameterFromQuery(Name = "q")]
    public string? Q { get; set; }

    protected List<SearchResultDto> Results { get; private set; } = new();
    protected bool Loading { get; private set; } = true;

    protected override async Task OnParametersSetAsync()
    {
        Loading = true;
        if (string.IsNullOrWhiteSpace(Q))
        {
            Results = new List<SearchResultDto>();
            Loading = false;
            return;
        }

        var result = await DocService.SearchAsync(Q);
        Results = result.OkNotNull ? result.Value : new List<SearchResultDto>();
        Loading = false;
    }
}
