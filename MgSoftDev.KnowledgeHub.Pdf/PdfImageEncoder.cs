using MgSoftDev.KnowledgeHub.Dtos;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;

namespace MgSoftDev.KnowledgeHub.Pdf;

/// <summary>
/// Turns a stored image into something MigraDoc will actually embed.
///
/// <b>This step is not optional and its absence is invisible.</b> KnowledgeHub stores images as
/// WebP, and PDFsharp does not import WebP — but it does not complain either: hand it WebP bytes
/// and it produces a perfectly valid PDF with no image XObject at all. Verified by inspecting the
/// output: PNG gave <c>/Subtype /Image /Width 200 /Height 100</c>, the same document built from
/// WebP gave zero image objects and never threw. So everything is transcoded to PNG here, and a
/// failure to decode is reported to the caller instead of vanishing.
/// </summary>
internal static class PdfImageEncoder
{
    /// <summary>
    /// The <c>base64:</c> form MigraDoc accepts in place of a file path, or null when the bytes
    /// cannot be decoded — the caller then prints a visible placeholder.
    /// </summary>
    public static string? ToMigraDocName(PdfExportImage image, out (int Width, int Height) size)
    {
        size = (image.Width, image.Height);
        if (image.Content.Length == 0) return null;

        try
        {
            using var decoded = Image.Load(image.Content);
            size = (decoded.Width, decoded.Height);

            using var png = new MemoryStream();
            decoded.Save(png, new PngEncoder());
            return "base64:" + Convert.ToBase64String(png.ToArray());
        }
        catch (UnknownImageFormatException)
        {
            return null;
        }
        catch (InvalidImageContentException)
        {
            return null;
        }
    }
}
