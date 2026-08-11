using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Radzen;

namespace MgSoftDev.KnowledgeHub.Blazor.Components.Embedded;

/// <summary>
/// Two-column shell with a draggable divider between the navigation tree and the content. Both
/// hosting scenarios use it — <see cref="KnowledgeHubBrowser"/> and the portal's
/// <c>KnowledgeHubLayout</c> — so the column sizing lives in exactly one place.
///
/// Hosts do not usually render this directly, but it is available if you want the same tree/content
/// split around your own content:
///
/// <code>
/// &lt;KnowledgeHubSplitLayout&gt;
///     &lt;TreeContent&gt;&lt;KnowledgeHubNavTree /&gt;&lt;/TreeContent&gt;
///     &lt;MainContent&gt;@YourContent&lt;/MainContent&gt;
/// &lt;/KnowledgeHubSplitLayout&gt;
/// </code>
/// </summary>
public partial class KnowledgeHubSplitLayout : ComponentBase, IAsyncDisposable
{
    [Inject] private IJSRuntime JS { get; set; } = null!;

    /// <summary>The navigation tree column. Not rendered at all when <see cref="ShowTree"/> is false.</summary>
    [Parameter] public RenderFragment? TreeContent { get; set; }

    /// <summary>The content column, which takes whatever width the tree leaves.</summary>
    [Parameter] public RenderFragment? MainContent { get; set; }

    /// <summary>Render the tree column. With false the content pane takes the full width.</summary>
    [Parameter] public bool ShowTree { get; set; } = true;

    /// <summary>Size to the container (100%) instead of the viewport. See KnowledgeHubBrowser.Embedded.</summary>
    [Parameter] public bool Embedded { get; set; } = true;

    /// <summary>Starting width of the tree column; a stored width overrides it. Any CSS length.</summary>
    [Parameter] public string TreeSize { get; set; } = "320px";

    /// <summary>How narrow the tree can be dragged.</summary>
    [Parameter] public string TreeMinSize { get; set; } = "200px";

    /// <summary>How wide the tree can be dragged.</summary>
    [Parameter] public string TreeMaxSize { get; set; } = "60%";

    /// <summary>Show the collapse/expand arrows on the divider. Off by default.</summary>
    [Parameter] public bool TreeCollapsible { get; set; }

    /// <summary>localStorage key for the dragged width. Null or empty disables remembering.</summary>
    [Parameter] public string? TreeWidthStorageKey { get; set; }

    /// <summary>
    /// Anything else you put on the tag lands on the root element — typically a <c>class</c> of
    /// your own to override the height variables. Your class is appended to the module's, not
    /// substituted for it.
    /// </summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public IReadOnlyDictionary<string, object>? AdditionalAttributes { get; set; }

    private string RootCssClass
    {
        get
        {
            var css = Embedded ? "kh-split kh-embedded" : "kh-split";

            // Collapsible="false" stops the arrows from WORKING but Radzen still renders one on
            // the bar, so the modifier below is what actually hides them.
            if (!TreeCollapsible) css += " kh-split-nocollapse";

            return AdditionalAttributes?.TryGetValue("class", out var hostClass) == true &&
                   hostClass?.ToString() is { Length: > 0 } extra
                ? $"{css} {extra}"
                : css;
        }
    }

    private ElementReference _rootElement;
    private ElementReference _treeElement;
    private IJSObjectReference? _module;

    /// <summary>Width the splitter is currently built with. Changing it remounts the splitter.</summary>
    private string _treeSize = "320px";

    private bool _restored;

    protected override void OnParametersSet()
    {
        // Until the stored width has been restored, follow the configured one. Afterwards the
        // stored value owns the field, or a host that re-renders would undo the restore.
        if (!_restored) _treeSize = TreeSize;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender || _restored || !ShowTree || string.IsNullOrEmpty(TreeWidthStorageKey)) return;

        _restored = true;
        try
        {
            var module = await GetModuleAsync();
            var stored = await module.InvokeAsync<string?>("readSetting", TreeWidthStorageKey);
            if (string.IsNullOrWhiteSpace(stored)) return;

            // A width dragged on a wide screen can be wider than the container this instance got
            // (a narrower window, or a host panel smaller than the portal), which would leave the
            // content column at zero pixels.
            var fitted = await module.InvokeAsync<string>(
                "fitToContainer", _rootElement, stored, TreeMinSize, TreeMaxSize);

            if (!string.IsNullOrWhiteSpace(fitted) && fitted != _treeSize)
            {
                _treeSize = fitted;
                StateHasChanged();
            }
        }
        catch (JSException)
        {
            // Remembering the width is a convenience; the splitter works fine without it.
        }
        catch (InvalidOperationException)
        {
            // Prerendering, or the circuit went away mid-call: same story.
        }
    }

    /// <summary>
    /// Records how wide the user dragged the tree. The measurement comes from the DOM instead of
    /// the event's NewSize because the latter's unit is undocumented, while this is unambiguously
    /// CSS pixels and matches the format of <see cref="TreeSize"/>.
    /// </summary>
    private async Task RememberTreeWidthAsync(RadzenSplitterResizeEventArgs args)
    {
        if (!ShowTree || string.IsNullOrEmpty(TreeWidthStorageKey)) return;

        try
        {
            var module = await GetModuleAsync();
            var width = await module.InvokeAsync<double>("elementWidth", _treeElement);
            if (width > 0)
                await module.InvokeAsync<bool>("writeSetting", TreeWidthStorageKey,
                    $"{Math.Round(width)}px");
        }
        catch (JSException)
        {
            // Storage disabled (private window, some WebView2 origins): stop remembering, keep working.
        }
        catch (InvalidOperationException)
        {
        }
    }

    private async Task<IJSObjectReference> GetModuleAsync() =>
        _module ??= await JS.InvokeAsync<IJSObjectReference>(
            "import", "./_content/MgSoftDev.KnowledgeHub.Blazor/knowledgehub.js");

    /// <summary>
    /// Releases the imported module. Unlike the dialogs, this component lives as long as the page,
    /// so the reference is worth cleaning up.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_module is null) return;

        try
        {
            await _module.DisposeAsync();
        }
        catch (JSDisconnectedException)
        {
            // The circuit is already gone (browser closed, navigation): nothing left to release.
        }
        catch (JSException)
        {
        }

        _module = null;
    }
}
