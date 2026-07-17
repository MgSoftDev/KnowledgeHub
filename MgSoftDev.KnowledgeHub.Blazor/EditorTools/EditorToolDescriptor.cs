namespace MgSoftDev.KnowledgeHub.Blazor.EditorTools;

/// <summary>
/// One custom button of the HTML editor toolbar. The host registers descriptors through
/// KnowledgeHubBlazorOptions.EditorTools; the editor renders a RadzenHtmlEditorCustomTool
/// per descriptor and dispatches clicks to <see cref="ExecuteAsync"/>.
/// </summary>
public sealed class EditorToolDescriptor
{
    /// <summary>Unique command key.</summary>
    public required string CommandName { get; init; }

    /// <summary>Material Symbols icon name shown on the toolbar button.</summary>
    public required string Icon { get; init; }

    /// <summary>Tooltip.</summary>
    public required string Title { get; init; }

    /// <summary>
    /// Produces the HTML to insert at the caret, or null to insert nothing (e.g. the user
    /// cancelled a dialog). May open dialogs through the context's DialogService.
    /// </summary>
    public required Func<EditorToolContext, Task<string?>> ExecuteAsync { get; init; }
}
