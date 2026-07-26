using System.Net;
using System.Text;
using MgSoftDev.KnowledgeHub.Blazor.Components.Dialogs;
using Microsoft.Extensions.DependencyInjection;
using Radzen;

namespace MgSoftDev.KnowledgeHub.Blazor.EditorTools;

/// <summary>
/// Built-in tool that resizes the image currently selected in the editor. Public so a host can
/// reuse it (e.g. re-register it under a different icon after removing the default).
/// </summary>
public static class ImageSizeTool
{
    /// <summary>Attributes read off the selected element. Names must match the requested attributes.</summary>
    public sealed class SelectedImage
    {
        public string? Src { get; set; }
        public string? Alt { get; set; }
        public string? Width { get; set; }
        public string? Height { get; set; }
        public string? Style { get; set; }
    }

    /// <summary>Reads the selected image, asks for the new size, and returns the replacement tag.</summary>
    public static async Task<string?> ExecuteAsync(EditorToolContext ctx)
    {
        SelectedImage? current = null;
        try
        {
            current = await ctx.Editor.GetSelectionAttributes<SelectedImage>(
                "img", ["src", "alt", "width", "height", "style"]);
        }
        catch (Exception)
        {
            // No selection / not an image: handled below like an empty result.
        }

        if (string.IsNullOrWhiteSpace(current?.Src))
        {
            ctx.Services.GetService<NotificationService>()?.Notify(new NotificationMessage
            {
                Severity = NotificationSeverity.Info,
                Summary = "Selecciona una imagen",
                Detail = "Haz clic sobre la imagen que quieres redimensionar y vuelve a pulsar el botón."
            });
            return null;
        }

        var spec = ImageSizeSpec.FromAttributes(current.Src, current.Alt, current.Style,
            current.Width, current.Height);

        var result = await ctx.Dialog.OpenAsync<ImageSizeDialog>("Tamaño de la imagen",
            new Dictionary<string, object?> { [nameof(ImageSizeDialog.Spec)] = spec },
            new DialogOptions { Width = "560px", Resizable = false });

        if (result is not ImageSizeSpec applied) return null;

        // Opening the dialog moved the focus away; put the selection back so InsertHtml replaces
        // the image instead of appending at an arbitrary caret position.
        await ctx.Editor.RestoreSelectionAsync();

        return BuildImgTag(applied);
    }

    /// <summary>
    /// Emits width/height/zoom as CSS in the style attribute and NO width/height HTML attributes —
    /// that mix is what made sizes unpredictable with the stock Radzen dialog.
    /// </summary>
    private static string BuildImgTag(ImageSizeSpec spec)
    {
        var sb = new StringBuilder("<img src=\"").Append(WebUtility.HtmlEncode(spec.Src)).Append('"');

        if (!string.IsNullOrWhiteSpace(spec.Alt))
            sb.Append(" alt=\"").Append(WebUtility.HtmlEncode(spec.Alt)).Append('"');

        var style = ImageSizeSpec.BuildStyle(spec);
        if (!string.IsNullOrWhiteSpace(style))
            sb.Append(" style=\"").Append(WebUtility.HtmlEncode(style)).Append('"');

        return sb.Append('>').ToString();
    }
}
