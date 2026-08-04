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

    /// <summary>
    /// Optional: when it returns true the button is drawn pressed, like Bold is while the caret
    /// sits on bold text. Use it for tools that toggle a mode instead of performing a one-off
    /// action; several tools sharing the same state make a radio group. Re-evaluated after every
    /// tool click, so the state must live outside the descriptor (this list is a singleton).
    /// </summary>
    public Func<IServiceProvider, bool>? IsSelected { get; init; }

    /// <summary>
    /// Optional: return false to hide the button entirely, e.g. a tool that depends on a service
    /// the host never registered. Defaults to always visible.
    /// </summary>
    public Func<IServiceProvider, bool>? IsVisible { get; init; }
}
