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
        // --- Cleanup level: a radio group. They only switch the mode used when pasting and by the
        // broom below; they never touch the document by themselves, so a stray click is harmless.
        CleanupLevelTool(HtmlCleanupLevel.Standard, "CleanupLevelStandard", "format_paint",
            "Limpieza estándar: quita basura de Word y scripts, conserva el formato"),
        CleanupLevelTool(HtmlCleanupLevel.Strict, "CleanupLevelStrict", "format_color_reset",
            "Limpieza media: además quita colores, fuentes y espaciados (conserva imágenes y avisos)"),
        CleanupLevelTool(HtmlCleanupLevel.PlainText, "CleanupLevelPlainText", "text_fields",
            "Limpieza máxima: solo texto, saltos de línea e imágenes"),

        new EditorToolDescriptor
        {
            CommandName = "SanitizeHtml",
            Icon = "cleaning_services",
            Title = "Aplicar la limpieza a todo el documento",
            IsVisible = HasSanitizer,
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

                var level = CurrentLevel(ctx.Services);
                var current = ctx.GetHtml();
                var clean = sanitizer.Sanitize(current ?? string.Empty, HtmlSanitizeContext.Manual, level);

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

    /// <summary>
    /// One of the three level buttons. They behave as a radio group because they all read and
    /// write the same <see cref="KnowledgeHubUiState.CleanupLevel"/>, so exactly one is drawn
    /// pressed. They return null: choosing a level must never modify the document by itself.
    /// </summary>
    private static EditorToolDescriptor CleanupLevelTool(HtmlCleanupLevel level, string commandName,
        string icon, string title) =>
        new()
        {
            CommandName = commandName,
            Icon = icon,
            Title = title,
            IsVisible = HasSanitizer,
            IsSelected = services => CurrentLevel(services) == level,
            ExecuteAsync = ctx =>
            {
                if (ctx.Services.GetService<KnowledgeHubUiState>() is { } state)
                    state.CleanupLevel = level;
                return Task.FromResult<string?>(null);
            }
        };

    /// <summary>Level buttons and the broom are pointless without a sanitizer, so they hide.</summary>
    private static bool HasSanitizer(IServiceProvider services) =>
        services.GetService<IKnowledgeHubHtmlSanitizer>() is not null;

    private static HtmlCleanupLevel CurrentLevel(IServiceProvider services) =>
        services.GetService<KnowledgeHubUiState>()?.CleanupLevel ?? HtmlCleanupLevel.Standard;
}
