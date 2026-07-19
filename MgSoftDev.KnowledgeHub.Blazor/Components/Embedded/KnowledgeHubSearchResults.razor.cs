using MgSoftDev.KnowledgeHub.Contracts;
using MgSoftDev.KnowledgeHub.Dtos;
using Microsoft.AspNetCore.Components;

namespace MgSoftDev.KnowledgeHub.Blazor.Components.Embedded;

/// <summary>
/// Search results over published pages. Embeddable: pass the term through <see cref="Term"/>
/// (the routed wrapper page reads it from the ?q= query string).
/// </summary>
public partial class KnowledgeHubSearchResults : ComponentBase
{
    [Inject] private IKnowledgeHubPageService DocService { get; set; } = null!;
    [Inject] private NavigationManager Nav { get; set; } = null!;

    /// <summary>The search term.</summary>
    [Parameter] public string? Term { get; set; }

    /// <summary>Without a handler, the result links navigate to /kh/page/{pk}.</summary>
    [Parameter] public EventCallback<Guid> OnPageSelected { get; set; }

    protected List<SearchResultDto> Results { get; private set; } = new();
    protected bool Loading { get; private set; } = true;

    protected override async Task OnParametersSetAsync()
    {
        Loading = true;
        if (string.IsNullOrWhiteSpace(Term))
        {
            Results = new List<SearchResultDto>();
            Loading = false;
            return;
        }

        var result = await DocService.SearchAsync(Term);
        Results = result.OkNotNull ? result.Value : new List<SearchResultDto>();
        Loading = false;
    }

    private async Task GoPage(Guid pagePk)
    {
        if (OnPageSelected.HasDelegate) await OnPageSelected.InvokeAsync(pagePk);
    }
}
