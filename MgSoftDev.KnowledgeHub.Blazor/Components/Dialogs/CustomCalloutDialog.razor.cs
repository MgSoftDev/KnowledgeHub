using Microsoft.AspNetCore.Components;
using Radzen;

namespace MgSoftDev.KnowledgeHub.Blazor.Components.Dialogs;

public partial class CustomCalloutDialog : ComponentBase
{
    [Inject] private DialogService Dialog { get; set; } = null!;

    protected CalloutSpec Spec { get; } = new();
    protected ColorScheme SelectedScheme { get; set; } = null!;

    protected static readonly string[] Emojis = { "💡", "⚠️", "❗", "✅", "ℹ️", "📌", "🔒", "🚀", "🐛", "📊" };

    protected static readonly List<ColorScheme> Schemes = new()
    {
        new ColorScheme("Azul", "#eff6ff", "#bfdbfe", "#3b82f6"),
        new ColorScheme("Ámbar", "#fffbeb", "#fde68a", "#f59e0b"),
        new ColorScheme("Rojo", "#fef2f2", "#fecaca", "#ef4444"),
        new ColorScheme("Verde", "#ecfdf5", "#a7f3d0", "#10b981"),
        new ColorScheme("Morado", "#f5f3ff", "#ddd6fe", "#8b5cf6"),
        new ColorScheme("Gris", "#f8fafc", "#e2e8f0", "#64748b")
    };

    protected override void OnInitialized()
    {
        SelectedScheme = Schemes[0];
        ApplyScheme();
    }

    private void OnSchemeChange() => ApplyScheme();

    private void ApplyScheme()
    {
        Spec.Bg = SelectedScheme.Bg;
        Spec.Border = SelectedScheme.Border;
        Spec.Accent = SelectedScheme.Accent;
    }

    protected string PreviewHtml =>
        $"<div style=\"background:{Spec.Bg};border:1px solid {Spec.Border};border-left:5px solid {Spec.Accent};border-radius:8px;padding:16px;\">" +
        $"<strong>{Spec.Icon} {System.Net.WebUtility.HtmlEncode(Spec.Title)}:</strong> {System.Net.WebUtility.HtmlEncode(Spec.Text)}</div>";

    private void Insert() => Dialog.Close(Spec);
    private void Cancel() => Dialog.Close(null);
}

/// <summary>Payload produced by the dialog and consumed by the CalloutCustom editor tool.</summary>
public sealed class CalloutSpec
{
    public string Icon { get; set; } = "💡";
    public string Title { get; set; } = "Nota";
    public string Text { get; set; } = "Escribe tu texto aquí.";
    public string Bg { get; set; } = "#eff6ff";
    public string Border { get; set; } = "#bfdbfe";
    public string Accent { get; set; } = "#3b82f6";
}

public sealed record ColorScheme(string Name, string Bg, string Border, string Accent);
