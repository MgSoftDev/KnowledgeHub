using MgSoftDev.KnowledgeHub.Contracts;
using Radzen;
using Radzen.Blazor;

namespace MgSoftDev.KnowledgeHub.Blazor.EditorTools;

/// <summary>Services available to a custom editor tool while executing.</summary>
public sealed class EditorToolContext
{
    /// <summary>Radzen dialog service (open configuration dialogs like the custom callout).</summary>
    public required DialogService Dialog { get; init; }

    /// <summary>Scoped service provider of the current UI scope.</summary>
    public required IServiceProvider Services { get; init; }

    /// <summary>The signed-in user, for tools that adapt to permissions.</summary>
    public required IKnowledgeHubUserContext User { get; init; }

    /// <summary>
    /// The live editor, for tools that work on the current SELECTION instead of just inserting at
    /// the caret — e.g. <c>GetSelectionAttributes&lt;T&gt;("img", …)</c> to read the selected image.
    /// The page editor calls <c>SaveSelectionAsync()</c> before dispatching, so a tool that opens a
    /// dialog must call <c>RestoreSelectionAsync()</c> before returning its replacement HTML
    /// (opening a dialog moves the focus and would otherwise lose the selection).
    /// </summary>
    public required RadzenHtmlEditor Editor { get; init; }

    /// <summary>Current HTML of the whole document, as bound by the page editor.</summary>
    public required Func<string> GetHtml { get; init; }

    /// <summary>
    /// The document as it stands RIGHT NOW, which in the code view is not the same thing as
    /// <see cref="GetHtml"/>. Radzen propagates the code view on blur, and a toolbar button
    /// deliberately keeps the focus, so the bound value can still hold the text from before the
    /// author started typing. Any tool that rewrites the whole document must use this one.
    /// </summary>
    public required Func<Task<string>> GetHtmlAsync { get; init; }

    /// <summary>
    /// Replaces the WHOLE document, for tools that reformat everything rather than insert at the
    /// caret (the built-in cleanup tool uses it). Return null from <c>ExecuteAsync</c> afterwards
    /// so nothing extra is inserted.
    /// </summary>
    public required Func<string, Task> ReplaceAllAsync { get; init; }
}
