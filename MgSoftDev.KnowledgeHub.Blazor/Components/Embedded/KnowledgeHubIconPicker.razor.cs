using Microsoft.AspNetCore.Components;

namespace MgSoftDev.KnowledgeHub.Blazor.Components.Embedded;

/// <summary>
/// Icon + color picker for a documentation page, modeled on the host's LocationIconPicker.
/// A themed grid of Material Symbols (docs / notices / technical / tags / users), a textbox to
/// type any other Material Symbol name, and a curated color row. Two-way bindable through
/// <see cref="Icon"/> / <see cref="IconColor"/> (<c>@bind-Icon</c> / <c>@bind-IconColor</c>).
/// Presentational only: it never persists — the host saves via its own "Guardar icono" button.
/// </summary>
public partial class KnowledgeHubIconPicker : ComponentBase
{
    /// <summary>Selected Material Symbols name (null = no icon).</summary>
    [Parameter] public string? Icon { get; set; }
    [Parameter] public EventCallback<string?> IconChanged { get; set; }

    /// <summary>Selected color as CSS/hex (null = inherit the surrounding text color).</summary>
    [Parameter] public string? IconColor { get; set; }
    [Parameter] public EventCallback<string?> IconColorChanged { get; set; }

    /// <summary>Disables every control (e.g. while a save is running or without edit rights).</summary>
    [Parameter] public bool Disabled { get; set; }

    // Themed catalog around documentation / notices / technical / tags / users.
    internal static readonly IReadOnlyList<IconGroup> Groups = new List<IconGroup>
    {
        new("Documentos", new[]
        {
            "article", "description", "menu_book", "book", "auto_stories", "topic",
            "folder", "folder_open", "snippet_folder", "sticky_note_2", "checklist", "task", "rule","label", "sell", "bookmark", "category", "layers", "flag"
        }),
        new("Otros", new[]
        {
            "policy", "gavel", "help", "info", "lightbulb", "warning", "priority_high", "code", "terminal", "bug_report", "settings", "build", "database","person", "group", "badge", "verified"
        })
    };

    // First entry (null) clears the color → the icon inherits the surrounding text color.
    internal static readonly IReadOnlyList<string?> Colors = new string?[]
    {
        null, "#2563eb", "#0891b2", "#059669", "#65a30d", "#ca8a04",
        "#ea580c", "#dc2626", "#db2777", "#7c3aed", "#475569", "#111827"
    };

    private async Task SelectIcon(string icon)
    {
        if (Disabled) return;
        Icon = string.Equals(Icon, icon, StringComparison.Ordinal) ? null : icon;
        await IconChanged.InvokeAsync(Icon);
    }

    private async Task OnCustomIconChanged(string? value)
    {
        Icon = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        await IconChanged.InvokeAsync(Icon);
    }

    private async Task SelectColor(string? color)
    {
        if (Disabled) return;
        IconColor = color;
        await IconColorChanged.InvokeAsync(IconColor);
    }

    private bool IsIconSelected(string icon) => string.Equals(Icon, icon, StringComparison.Ordinal);
    private bool IsColorSelected(string? color) => string.Equals(IconColor, color, StringComparison.OrdinalIgnoreCase);

    internal sealed record IconGroup(string Title, IReadOnlyList<string> Icons);
}
