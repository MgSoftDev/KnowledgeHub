namespace MgSoftDev.KnowledgeHub.Dtos;

/// <summary>
/// One heading of a page's content, as the "En esta página" panel lists it.
/// </summary>
/// <param name="Level">1..6, straight from the tag name.</param>
/// <param name="Text">Plain text of the heading: markup stripped, entities decoded.</param>
/// <param name="Id">Anchor id, unique within the page and present in <see cref="HtmlOutline.Html"/>.</param>
public sealed record HtmlHeading(int Level, string Text, string Id);

/// <summary>
/// Content ready to render plus its headings, in document order.
/// <para>
/// The anchors are injected HERE, at display time, and are never stored: <c>id</c> is not in the
/// sanitizer's allow-list —all three cleanup levels drop it— and every save sanitizes, so an anchor
/// written into the content would survive exactly until the next save and then vanish silently.
/// </para>
/// </summary>
public sealed record HtmlOutline(string Html, IReadOnlyList<HtmlHeading> Headings)
{
    /// <summary>Nothing to link: the html comes back untouched, same reference and all.</summary>
    public static HtmlOutline Unchanged(string html) => new(html, []);
}
