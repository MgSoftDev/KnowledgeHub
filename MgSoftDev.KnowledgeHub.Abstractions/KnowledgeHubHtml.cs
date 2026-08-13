using System.Net;
using System.Text.RegularExpressions;

namespace MgSoftDev.KnowledgeHub;

/// <summary>
/// Central definitions of the HTML image reference formats. Stored HTML always holds stable
/// <c>docimg://{DocImage.Pk}</c> references; display HTML holds URLs ending in
/// <c>{contentHash}.webp</c>. Keeping every regex here prevents the drift the original demo
/// had (the same patterns duplicated across services).
/// </summary>
public static partial class KnowledgeHubHtml
{
    /// <summary>Scheme of the stable stored image reference.</summary>
    public const string DocImgScheme = "docimg://";

    /// <summary>Builds the stable stored reference for an image.</summary>
    public static string DocImgUrl(Guid imagePk) => $"{DocImgScheme}{imagePk}";

    /// <summary>Matches stored references: <c>docimg://{guid}</c> (group "pk").</summary>
    [GeneratedRegex(@"docimg://(?<pk>[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12})")]
    public static partial Regex DocImgRegex();

    /// <summary>
    /// Matches any display URL by its mandatory <c>{sha256hex}.webp</c> suffix (group "hash"),
    /// capturing the whole URL so it can be replaced back to <c>docimg://{pk}</c>. Base-agnostic
    /// on purpose: works for WPF virtual-host URLs, relative server endpoints and absolute API URLs.
    /// </summary>
    [GeneratedRegex(@"[^\s""'<>()]*(?<hash>[0-9a-fA-F]{64})\.webp")]
    public static partial Regex DisplayUrlRegex();

    /// <summary>Matches pasted inline images: <c>data:image/...;base64,...</c> (groups "mime", "data").</summary>
    [GeneratedRegex("data:(?<mime>image/[a-zA-Z0-9.+-]+);base64,(?<data>[A-Za-z0-9+/=]+)")]
    public static partial Regex DataUriRegex();

    /// <summary>Matches any HTML tag; used to strip markup when building plain-text snippets.</summary>
    [GeneratedRegex("<[^>]+>")]
    public static partial Regex HtmlTagRegex();

    /// <summary>
    /// Elements that carry content without contributing any text. Without this list a page holding
    /// nothing but a diagram would be judged empty, because stripping the tags leaves nothing.
    /// </summary>
    private static readonly string[] VisualTags =
        ["<img", "<iframe", "<video", "<embed", "<object", "<svg", "<canvas"];

    /// <summary>
    /// True when the html would show nothing on screen: no text and no visual element. Used to keep
    /// a page that was created and published but never written from becoming a heading followed by
    /// a blank sheet in an exported PDF.
    /// <para>
    /// Two traps this deliberately avoids. First, <c>&lt;p&gt;&lt;br&gt;&lt;/p&gt;</c> is NOT a marker of
    /// emptiness: the library appends it to every callout so the caret lands outside the box, so the
    /// question has to be "is there any text left once the tags are gone", never "does it contain
    /// that fragment". Second, a page whose only content is an image has no text at all and is very
    /// much not empty — hence <see cref="VisualTags"/> and the <c>docimg://</c> check.
    /// </para>
    /// </summary>
    public static bool IsVisuallyEmpty(string? html)
    {
        if (string.IsNullOrWhiteSpace(html)) return true;

        if (html.Contains(DocImgScheme, StringComparison.OrdinalIgnoreCase)) return false;
        foreach (var tag in VisualTags)
            if (html.Contains(tag, StringComparison.OrdinalIgnoreCase)) return false;

        // Decoding matters: cleaning at level 2 or 3 can leave a paragraph holding only &nbsp;,
        // which is text to a plain comparison and blank to a reader. Decoded it becomes U+00A0,
        // which IsNullOrWhiteSpace does treat as whitespace.
        var text = WebUtility.HtmlDecode(HtmlTagRegex().Replace(html, " "));
        return string.IsNullOrWhiteSpace(text);
    }

    /// <summary>Distinct DocImage pks referenced by <paramref name="html"/> via docimg:// links.</summary>
    public static IReadOnlyList<Guid> ExtractDocImagePks(string html) =>
        DocImgRegex().Matches(html)
            .Select(m => Guid.Parse(m.Groups["pk"].Value))
            .Distinct()
            .ToList();
}
