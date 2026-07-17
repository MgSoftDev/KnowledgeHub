using MgSoftDev.KnowledgeHub.Blazor.Components.Dialogs;
using Radzen;

namespace MgSoftDev.KnowledgeHub.Blazor.EditorTools;

/// <summary>
/// The four callout tools KnowledgeHub ships with. They are registered through the SAME
/// mechanism host tools use (KnowledgeHubBlazorOptions.EditorTools), so hosts can remove or
/// replace them freely.
/// </summary>
public static class BuiltInEditorTools
{
    public static List<EditorToolDescriptor> CreateDefaults() => new()
    {
        new EditorToolDescriptor
        {
            CommandName = "CalloutNota",
            Icon = "lightbulb",
            Title = "Insertar nota",
            ExecuteAsync = _ => Task.FromResult<string?>(
                CalloutHtml.Build("#eff6ff", "#bfdbfe", "#3b82f6", "💡", "Nota", "Escribe tu nota aquí."))
        },
        new EditorToolDescriptor
        {
            CommandName = "CalloutAdvertencia",
            Icon = "warning",
            Title = "Insertar advertencia",
            ExecuteAsync = _ => Task.FromResult<string?>(
                CalloutHtml.Build("#fffbeb", "#fde68a", "#f59e0b", "⚠️", "Advertencia", "Escribe tu advertencia aquí."))
        },
        new EditorToolDescriptor
        {
            CommandName = "CalloutImportante",
            Icon = "priority_high",
            Title = "Insertar importante",
            ExecuteAsync = _ => Task.FromResult<string?>(
                CalloutHtml.Build("#fef2f2", "#fecaca", "#ef4444", "❗", "Importante", "Escribe el punto importante aquí."))
        },
        new EditorToolDescriptor
        {
            CommandName = "CalloutCustom",
            Icon = "palette",
            Title = "Aviso personalizado…",
            ExecuteAsync = async ctx =>
            {
                var result = await ctx.Dialog.OpenAsync<CustomCalloutDialog>("Aviso personalizado",
                    parameters: null,
                    new DialogOptions { Width = "540px", Resizable = false });

                return result is CalloutSpec spec
                    ? CalloutHtml.Build(spec.Bg, spec.Border, spec.Accent, spec.Icon, spec.Title, spec.Text)
                    : null;
            }
        }
    };
}
