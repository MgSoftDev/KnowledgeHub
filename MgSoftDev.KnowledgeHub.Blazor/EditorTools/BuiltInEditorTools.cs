using MgSoftDev.KnowledgeHub.Blazor.Components.Dialogs;
using MgSoftDev.KnowledgeHub.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Radzen;

namespace MgSoftDev.KnowledgeHub.Blazor.EditorTools;

/// <summary>
/// The tools KnowledgeHub ships with: four callouts, the image size dialog and the HTML cleanup
/// button. They are registered through the SAME mechanism host tools use
/// (KnowledgeHubBlazorOptions.EditorTools), so hosts can remove or replace them freely.
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
        },
        new EditorToolDescriptor
        {
            CommandName = "ImageSize",
            Icon = "photo_size_select_large",
            Title = "Tamaño de la imagen seleccionada…",
            ExecuteAsync = ImageSizeTool.ExecuteAsync
        },
        new EditorToolDescriptor
        {
            CommandName = "SanitizeHtml",
            Icon = "cleaning_services",
            Title = "Limpiar el HTML del documento",
            ExecuteAsync = async ctx =>
            {
                var sanitizer = ctx.Services.GetService<IKnowledgeHubHtmlSanitizer>();
                if (sanitizer is null)
                {
                    ctx.Services.GetService<NotificationService>()?.Notify(new NotificationMessage
                    {
                        Severity = NotificationSeverity.Warning,
                        Summary = "Limpieza no disponible",
                        Detail = "No hay ningún sanitizador registrado. Añade el paquete " +
                                 "MgSoftDev.KnowledgeHub.HtmlSanitizer y llama a AddKnowledgeHubHtmlSanitizer()."
                    });
                    return null;
                }

                var current = ctx.GetHtml();
                var clean = sanitizer.Sanitize(current ?? string.Empty, HtmlSanitizeContext.Manual);

                var notify = ctx.Services.GetService<NotificationService>();
                if (clean == current)
                {
                    notify?.Notify(new NotificationMessage
                    {
                        Severity = NotificationSeverity.Info,
                        Summary = "Nada que limpiar",
                        Detail = "El documento ya está limpio."
                    });
                    return null;
                }

                await ctx.ReplaceAllAsync(clean);
                notify?.Notify(new NotificationMessage
                {
                    Severity = NotificationSeverity.Success,
                    Summary = "HTML limpiado",
                    Detail = $"Se quitaron {current!.Length - clean.Length} caracteres de marcado no permitido."
                });

                // The whole document was replaced already; nothing to insert at the caret.
                return null;
            }
        }
    };
}
