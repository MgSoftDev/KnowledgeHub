using MgSoftDev.KnowledgeHub.Contracts;
using Radzen;

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
}
