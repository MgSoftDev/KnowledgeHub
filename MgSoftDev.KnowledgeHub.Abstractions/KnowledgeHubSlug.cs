using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace MgSoftDev.KnowledgeHub;

/// <summary>
/// Turns a page title into its slug. The slug is an internal identifier: it is never shown, never
/// part of a URL (routes go by <c>Guid</c>) and nothing is ever looked up by it — so callers do NOT
/// need to worry about collisions. <c>CreatePageAsync</c> derives the slug from the title and adds
/// a numeric suffix when the base is taken, which is why two pages may share a title.
/// </summary>
public static class KnowledgeHubSlug
{
    /// <summary>Fallback for titles made only of characters a slug cannot carry (e.g. "★ ★ ★").</summary>
    public const string Fallback = "pagina";

    private static readonly Regex NonSlugChars = new("[^a-z0-9]+", RegexOptions.Compiled);

    /// <summary>
    /// Lower-cases, strips accents and collapses everything else into single dashes:
    /// <c>"Línea 1 — Producción"</c> becomes <c>"linea-1-produccion"</c>.
    /// </summary>
    public static string Slugify(string title)
    {
        if (string.IsNullOrWhiteSpace(title)) return Fallback;

        // FormD splits an accented letter into letter + combining mark, so dropping the marks
        // leaves the plain ASCII letter behind instead of losing the whole character.
        var normalized = title.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);
        foreach (var character in normalized)
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
                builder.Append(character);

        var slug = NonSlugChars.Replace(builder.ToString().Normalize(NormalizationForm.FormC), "-").Trim('-');
        return string.IsNullOrEmpty(slug) ? Fallback : slug;
    }
}
