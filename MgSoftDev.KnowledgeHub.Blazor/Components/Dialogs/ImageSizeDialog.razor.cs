using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Radzen;

namespace MgSoftDev.KnowledgeHub.Blazor.Components.Dialogs;

/// <summary>
/// Size/alt editor for the image selected in the HTML editor. Unlike Radzen's built-in image
/// dialog it shows the image's REAL pixel size (so you are not guessing when shrinking it) and
/// writes width/height as CSS in the style attribute instead of as HTML attributes.
/// </summary>
public partial class ImageSizeDialog : ComponentBase, IAsyncDisposable
{
    [Inject] private DialogService Dialog { get; set; } = null!;
    [Inject] private IJSRuntime JS { get; set; } = null!;

    /// <summary>Current attributes of the selected image.</summary>
    [Parameter] public ImageSizeSpec Spec { get; set; } = new();

    private IJSObjectReference? _module;

    /// <summary>Intrinsic size, read from the browser; null while loading or if it failed.</summary>
    protected int? NaturalWidth { get; private set; }
    protected int? NaturalHeight { get; private set; }
    protected bool SizeLoading { get; private set; } = true;

    /// <summary>When set, editing one dimension recomputes the other from the intrinsic ratio.</summary>
    protected bool KeepRatio { get; set; } = true;

    protected override async Task OnInitializedAsync()
    {
        try
        {
            _module = await JS.InvokeAsync<IJSObjectReference>(
                "import", "./_content/MgSoftDev.KnowledgeHub.Blazor/knowledgehub.js");
            var size = await _module.InvokeAsync<NaturalSize?>("imageNaturalSize", Spec.Src);
            if (size is not null && size.Width > 0)
            {
                NaturalWidth = size.Width;
                NaturalHeight = size.Height;
            }
        }
        catch (JSException)
        {
            // Intrinsic size is a convenience: the dialog stays usable without it.
        }
        finally
        {
            SizeLoading = false;
        }
    }

    private double? Ratio =>
        NaturalWidth is > 0 && NaturalHeight is > 0 ? NaturalHeight.Value / (double)NaturalWidth.Value : null;

    protected void OnWidthChanged(int? value)
    {
        Spec.Width = value;
        if (KeepRatio && value is > 0 && Ratio is { } r) Spec.Height = (int)Math.Round(value.Value * r);
    }

    protected void OnHeightChanged(int? value)
    {
        Spec.Height = value;
        if (KeepRatio && value is > 0 && Ratio is { } r && r > 0) Spec.Width = (int)Math.Round(value.Value / r);
    }

    /// <summary>Sets width/height back to the image's intrinsic size.</summary>
    protected void UseNaturalSize()
    {
        if (NaturalWidth is null) return;
        Spec.Width = NaturalWidth;
        Spec.Height = NaturalHeight;
    }

    /// <summary>Clears the size so the image renders at its natural size again.</summary>
    protected void ClearSize()
    {
        Spec.Width = null;
        Spec.Height = null;
        Spec.Zoom = null;
    }

    protected string PreviewStyle => ImageSizeSpec.BuildStyle(Spec, forPreview: true);

    private void Apply() => Dialog.Close(Spec);
    private void Cancel() => Dialog.Close(null);

    public async ValueTask DisposeAsync()
    {
        if (_module is null) return;
        try { await _module.DisposeAsync(); }
        catch (JSDisconnectedException) { /* circuit already gone */ }
    }

    private sealed class NaturalSize
    {
        public int Width { get; set; }
        public int Height { get; set; }
    }
}

/// <summary>Payload exchanged between the image size tool and its dialog.</summary>
public sealed partial class ImageSizeSpec
{
    public string? Src { get; set; }
    public string? Alt { get; set; }

    /// <summary>CSS width in pixels; null leaves it unset.</summary>
    public int? Width { get; set; }

    /// <summary>CSS height in pixels; null leaves it unset.</summary>
    public int? Height { get; set; }

    /// <summary>CSS zoom in percent; null leaves it unset.</summary>
    public int? Zoom { get; set; }

    /// <summary>Everything already in the style attribute that this dialog does not manage.</summary>
    public string? OtherStyles { get; set; }

    [GeneratedRegex(@"(?<name>[-a-zA-Z]+)\s*:\s*(?<value>[^;]+)")]
    private static partial Regex StyleDeclarationRegex();

    [GeneratedRegex(@"^\s*(?<n>\d+(\.\d+)?)\s*(px)?\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex PixelValueRegex();

    [GeneratedRegex(@"^\s*(?<n>\d+(\.\d+)?)\s*%\s*$")]
    private static partial Regex PercentValueRegex();

    /// <summary>
    /// Reads the current values off the selected image. Width/height are taken from the style
    /// first and from the HTML attributes as a fallback, because Radzen's own image dialog writes
    /// them as attributes — this is what lets us migrate them into the style.
    /// </summary>
    public static ImageSizeSpec FromAttributes(string? src, string? alt, string? style,
        string? widthAttribute, string? heightAttribute)
    {
        var spec = new ImageSizeSpec { Src = src, Alt = alt };
        var others = new StringBuilder();

        foreach (Match m in StyleDeclarationRegex().Matches(style ?? string.Empty))
        {
            var name = m.Groups["name"].Value.Trim().ToLowerInvariant();
            var value = m.Groups["value"].Value.Trim();

            switch (name)
            {
                case "width": spec.Width = ParsePixels(value); break;
                case "height": spec.Height = ParsePixels(value); break;
                case "zoom": spec.Zoom = ParsePercent(value); break;
                default:
                    others.Append(name).Append(':').Append(value).Append(';');
                    break;
            }
        }

        spec.Width ??= ParsePixels(widthAttribute);
        spec.Height ??= ParsePixels(heightAttribute);
        spec.OtherStyles = others.Length > 0 ? others.ToString() : null;
        return spec;
    }

    private static int? ParsePixels(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var m = PixelValueRegex().Match(value);
        return m.Success && double.TryParse(m.Groups["n"].Value, NumberStyles.Float,
            CultureInfo.InvariantCulture, out var n) ? (int)Math.Round(n) : null;
    }

    private static int? ParsePercent(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var m = PercentValueRegex().Match(value);
        if (m.Success && double.TryParse(m.Groups["n"].Value, NumberStyles.Float,
            CultureInfo.InvariantCulture, out var pct)) return (int)Math.Round(pct);

        // "zoom: 2" is a factor, not a percentage.
        var factor = PixelValueRegex().Match(value);
        return factor.Success && double.TryParse(factor.Groups["n"].Value, NumberStyles.Float,
            CultureInfo.InvariantCulture, out var f) ? (int)Math.Round(f * 100) : null;
    }

    /// <summary>
    /// Builds the style attribute. Order matches what the host asked for: zoom, then width, then
    /// height, with any unmanaged declarations kept at the end.
    /// </summary>
    public static string BuildStyle(ImageSizeSpec spec, bool forPreview = false)
    {
        var sb = new StringBuilder();
        if (spec.Zoom is > 0) sb.Append("zoom:").Append(spec.Zoom.Value.ToString(CultureInfo.InvariantCulture)).Append("%;");
        if (spec.Width is > 0) sb.Append("width:").Append(spec.Width.Value.ToString(CultureInfo.InvariantCulture)).Append("px;");
        if (spec.Height is > 0) sb.Append("height:").Append(spec.Height.Value.ToString(CultureInfo.InvariantCulture)).Append("px;");

        // The preview lives in a small dialog: zoom there would blow the layout apart.
        if (forPreview && spec.Zoom is > 0)
        {
            sb.Clear();
            if (spec.Width is > 0) sb.Append("width:").Append(spec.Width.Value.ToString(CultureInfo.InvariantCulture)).Append("px;");
            if (spec.Height is > 0) sb.Append("height:").Append(spec.Height.Value.ToString(CultureInfo.InvariantCulture)).Append("px;");
        }

        if (!forPreview && !string.IsNullOrWhiteSpace(spec.OtherStyles)) sb.Append(spec.OtherStyles);
        return sb.ToString();
    }
}
