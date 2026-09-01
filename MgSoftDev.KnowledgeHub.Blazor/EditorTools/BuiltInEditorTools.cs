using MgSoftDev.KnowledgeHub.Blazor.Components.Dialogs;
using MgSoftDev.KnowledgeHub.Contracts;
using MgSoftDev.KnowledgeHub.Security;
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
            // Actúa sobre el documento entero, así que tiene sentido también mirando el código.
            EnabledModes = HtmlEditorMode.Design | HtmlEditorMode.Source,
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
                // Async: en la vista código el valor enlazado va por detrás de lo tecleado.
                var current = await ctx.GetHtmlAsync();
                var clean = sanitizer.Sanitize(current, HtmlSanitizeContext.Manual, level);

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
                    Detail = $"Se quitaron {current.Length - clean.Length} caracteres de marcado no permitido."
                });

                // The whole document was replaced already; nothing to insert at the caret.
                return null;
            }
        },
        new EditorToolDescriptor
        {
            CommandName = "FormatHtml",
            Icon = "format_indent_increase",
            Title = "Formatear el HTML para poder leerlo",
            IsVisible = HasFormatter,
            // Solo en la vista código: sangrar el editor visual no se ve por ningún lado.
            EnabledModes = HtmlEditorMode.Source,
            ExecuteAsync = async ctx =>
            {
                var formatter = ctx.Services.GetService<IKnowledgeHubHtmlFormatter>();
                var notify = ctx.Services.GetService<NotificationService>();
                if (formatter is null) return null;

                var current = await ctx.GetHtmlAsync();
                if (string.IsNullOrWhiteSpace(current)) return null;

                string formatted;
                try
                {
                    formatted = formatter.Format(current);
                }
                catch (Exception ex)
                {
                    // El formateador parsea HTML de verdad; si algo lo tumba, el autor tiene que
                    // enterarse y conservar su texto, no verlo desaparecer.
                    notify?.Notify(new NotificationMessage
                    {
                        Severity = NotificationSeverity.Error,
                        Summary = "No se pudo formatear",
                        Detail = ex.Message
                    });
                    return null;
                }

                if (formatted == current)
                {
                    notify?.Notify(new NotificationMessage
                    {
                        Severity = NotificationSeverity.Info,
                        Summary = "Nada que formatear",
                        Detail = "El HTML ya está como quedaría."
                    });
                    return null;
                }

                await ctx.ReplaceAllAsync(formatted);
                notify?.Notify(new NotificationMessage
                {
                    Severity = NotificationSeverity.Success,
                    Summary = "HTML formateado",
                    Detail = "Solo se añadieron saltos y sangría; lo que se ve no cambia.",
                    Duration = 2500
                });
                return null;
            }
        },

        new EditorToolDescriptor
        {
            CommandName = "TemplateModels",
            Icon = "data_object",
            Title = "Datos disponibles para esta página…",
            // Only for those who may make a page dynamic: to anyone else the dialog would list data
            // they can never use.
            IsVisible = services =>
                services.GetService<IKnowledgeHubUserContext>()?.CanUseTemplates() ?? false,
            ExecuteAsync = async ctx =>
            {
                await ctx.Dialog.OpenAsync<TemplateModelsDialog>("Datos disponibles",
                    parameters: null,
                    new DialogOptions { Width = "640px", Resizable = true });

                // A reference dialog: it copies to the clipboard, it never inserts at the caret.
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
            // Solo cambian el nivel que usarán pegar y la escoba: poder elegirlo y no poder
            // aplicarlo mientras miras el código no tendría sentido.
            EnabledModes = HtmlEditorMode.Design | HtmlEditorMode.Source,
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

    /// <summary>Sin formateador registrado el botón no tiene nada que hacer, así que se esconde.</summary>
    private static bool HasFormatter(IServiceProvider services) =>
        services.GetService<IKnowledgeHubHtmlFormatter>() is not null;

    private static HtmlCleanupLevel CurrentLevel(IServiceProvider services) =>
        services.GetService<KnowledgeHubUiState>()?.CleanupLevel ?? HtmlCleanupLevel.Standard;
}
