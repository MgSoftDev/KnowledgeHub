using MgSoftDev.KnowledgeHub.Contracts;
using MgSoftDev.KnowledgeHub.Dtos;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Radzen;

namespace MgSoftDev.KnowledgeHub.Blazor.Components.Dialogs;

public partial class TemplateModelsDialog : ComponentBase
{
    [Inject] private DialogService Dialog { get; set; } = null!;
    [Inject] private IKnowledgeHubPageService DocService { get; set; } = null!;
    [Inject] private NotificationService Notify { get; set; } = null!;
    [Inject] private IJSRuntime JS { get; set; } = null!;

    protected TemplateCatalogDto? Catalog { get; private set; }
    protected bool Loading { get; private set; } = true;
    protected string? ErrorMessage { get; private set; }

    /// <summary>
    /// The model the library always provides. Listed by hand because it is a contract, not data:
    /// kh.roles in particular means "list the system's roles" needs no provider at all.
    /// </summary>
    protected static readonly (string Expression, string Description)[] BuiltIns =
    [
        ("kh.roles", "Los roles del catálogo del anfitrión (name, display_name)"),
        ("kh.user.name", "Usuario que está leyendo"),
        ("kh.user.display_name", "Su nombre para mostrar"),
        ("kh.user.permissions", "Sus permisos, como lista"),
        ("kh.user.has \"Role.X\"", "Si tiene ese permiso — para mostrar un bloque solo a quien toca"),
        ("kh.page.title", "Título de esta página"),
        ("kh.page.slug", "Su slug"),
        ("kh.is_pdf", "Verdadero solo al exportar: {{ if !kh.is_pdf }}…{{ end }}")
    ];

    protected override async Task OnInitializedAsync()
    {
        var result = await DocService.GetTemplateCatalogAsync();
        if (result.OkNotNull) Catalog = result.Value;
        else ErrorMessage = result.UnfinishedInfo?.Title ?? "No se pudo cargar el catálogo.";
        Loading = false;
    }

    /// <summary>
    /// Copies the expression already wrapped in braces — what actually gets pasted into the page.
    /// Falls back to a notice showing it, because the clipboard API needs a secure context and is
    /// not available everywhere the module runs.
    /// </summary>
    protected async Task CopyAsync(string expression)
    {
        var snippet = $"{{{{ {expression} }}}}";
        try
        {
            await JS.InvokeVoidAsync("navigator.clipboard.writeText", snippet);
            Notify.Notify(new NotificationMessage
            {
                Severity = NotificationSeverity.Success,
                Summary = "Copiado",
                Detail = snippet,
                Duration = 2500
            });
        }
        catch (JSException)
        {
            Notify.Notify(new NotificationMessage
            {
                Severity = NotificationSeverity.Info,
                Summary = "Cópialo a mano",
                Detail = snippet,
                Duration = 6000
            });
        }
    }
}
