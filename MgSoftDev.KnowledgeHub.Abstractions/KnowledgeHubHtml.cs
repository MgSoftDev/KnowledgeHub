using System.Net;
using System.Text.RegularExpressions;
using MgSoftDev.KnowledgeHub.Dtos;

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

    // ------------------------------------------------------------------ índice de la página

    /// <summary>Prefix of every generated anchor, so a heading titled "Main" cannot take over the
    /// host's own <c>#main</c>. Everything the module puts in the page is prefixed the same way.</summary>
    public const string HeadingAnchorPrefix = "kh-";

    /// <summary>
    /// Matches a heading together with its content (groups "level", "attrs", "inner"). The attribute
    /// part consumes quoted values whole, so a <c>&gt;</c> inside <c>title="a &gt; b"</c> cannot end
    /// the tag early — with a naive <c>[^&gt;]*</c> the injected id would land in the middle of the
    /// author's markup. No backreference on the closing tag on purpose: the source generator bails
    /// out on some backreference shapes with SYSLIB1044, which is a build error here.
    /// </summary>
    [GeneratedRegex("""<h(?<level>[1-6])(?<attrs>(?:"[^"]*"|'[^']*'|[^'">])*)>(?<inner>.*?)</h[1-6]\s*>""",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Singleline)]
    private static partial Regex HeadingRegex();

    /// <summary>An <c>id</c> attribute already present on a tag (group "id"), quoted or bare.</summary>
    [GeneratedRegex("""\bid\s*=\s*(?:"(?<id>[^"]*)"|'(?<id>[^']*)'|(?<id>[^\s"'>]+))""",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex IdAttributeRegex();

    /// <summary>
    /// Lists the headings of a page and returns the same html with an anchor id on each of them, so
    /// the "En esta página" panel can link to them.
    /// <para>
    /// Anchors are generated HERE and never stored, because they could not be: <c>id</c> is not in
    /// the sanitizer's allow-list at any cleanup level and every save sanitizes, so an anchor typed
    /// into the content lives until the next save and then disappears without a word. That also
    /// makes the panel work on pages written long before it existed, with nothing to migrate.
    /// </para>
    /// <para>
    /// Deliberate omissions: a heading with no readable text (empty, only a &lt;br&gt;, only an
    /// image) gets neither an entry nor an id — a link with nothing on it is noise; and a heading
    /// whose closing tag never arrives is left completely alone, so malformed markup costs one
    /// missing link instead of broken html.
    /// </para>
    /// </summary>
    /// <param name="html">Display html, already image-rewritten and template-rendered.</param>
    /// <param name="maxLevel">Deepest heading that gets an entry (1..6). Deeper ones are untouched.</param>
    public static HtmlOutline BuildOutline(string? html, int maxLevel = 3)
    {
        if (string.IsNullOrEmpty(html)) return HtmlOutline.Unchanged(html ?? string.Empty);
        if (!html.Contains("<h", StringComparison.OrdinalIgnoreCase)) return HtmlOutline.Unchanged(html);

        var deepest = Math.Clamp(maxLevel, 1, 6);
        var headings = new List<HtmlHeading>();

        // Seeded with every id the document already carries: a host can allow `id` through its own
        // sanitizer configuration, and a generated anchor must not collide with one of those.
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match existing in IdAttributeRegex().Matches(html)) used.Add(existing.Groups["id"].Value);

        var rewritten = HeadingRegex().Replace(html, match =>
        {
            var level = match.Groups["level"].Value[0] - '0';
            if (level > deepest) return match.Value;

            var text = PlainText(match.Groups["inner"].Value);
            if (text.Length == 0) return match.Value;

            var attrs = match.Groups["attrs"];
            var present = IdAttributeRegex().Match(attrs.Value);
            if (present.Success)
            {
                // Reuse it. A second id would leave the browser honouring the first one and the
                // panel pointing at an anchor that resolves to nothing.
                headings.Add(new HtmlHeading(level, text, present.Groups["id"].Value));
                return match.Value;
            }

            var id = Unique(used, HeadingAnchorPrefix + KnowledgeHubSlug.Slugify(text));
            headings.Add(new HtmlHeading(level, text, id));

            // Only the opening tag is rebuilt; everything from '>' onwards is copied verbatim, so
            // the author's content and closing tag come out exactly as they went in.
            var afterOpenTag = attrs.Index - match.Index + attrs.Length;
            return $"<h{level}{attrs.Value} id=\"{id}\"{match.Value[afterOpenTag..]}";
        });

        return headings.Count == 0 ? HtmlOutline.Unchanged(html) : new HtmlOutline(rewritten, headings);
    }

    /// <summary>Readable text of a heading: markup out, entities decoded, whitespace collapsed.</summary>
    private static string PlainText(string inner)
    {
        var text = WebUtility.HtmlDecode(HtmlTagRegex().Replace(inner, " "));
        // Splitting on whitespace collapses the runs left by the stripped tags and, because
        // char.IsWhiteSpace covers U+00A0, drops a heading holding nothing but &nbsp;.
        return string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }

    /// <summary>Free anchor for the candidate, suffixed -2, -3… as the page slugs are.</summary>
    private static string Unique(HashSet<string> used, string candidate)
    {
        if (used.Add(candidate)) return candidate;

        for (var attempt = 2; ; attempt++)
        {
            var next = $"{candidate}-{attempt}";
            if (used.Add(next)) return next;
        }
    }
}
