using System.Text;
using MgSoftDev.KnowledgeHub.Contracts;
using MgSoftDev.KnowledgeHub.Dtos;
using MgSoftDev.ReturningCore;

namespace KnowledgeHub.ParityHarness;

/// <summary>
/// Stand-in renderer for the parity run.
///
/// The real engine is Chromium, so it depends on the machine having a browser — and the harness has
/// to give the SAME answer in the four store modes, on any machine. What these checks are about is
/// the pipeline and the permission filtering, not the drawing: whether a page reaches the exporter
/// is decided long before any renderer runs.
///
/// The real engine is still exercised once, separately, and says out loud when it cannot run.
/// </summary>
public sealed class FakePdfRenderer : IKnowledgeHubPdfRenderer
{
    public Task<Returning<byte[]>> RenderAsync(PdfExportDocument document)
    {
        // Cabecera de PDF válida más un resumen legible: si un check falla, el volcado dice qué
        // secciones e imágenes llegaron hasta aquí.
        var text = new StringBuilder("%PDF-1.7\n% arnés de paridad\n");
        text.Append("% título: ").Append(document.Title).Append('\n');
        text.Append("% secciones: ").Append(document.Sections.Count).Append('\n');
        text.Append("% imágenes: ").Append(document.Images.Count).Append('\n');
        foreach (var section in document.Sections)
            text.Append("%   [").Append(section.Level).Append("] ").Append(section.Title).Append('\n');
        text.Append(new string('.', 1200)).Append("\n%%EOF\n");   // > 1 KB, como pide el check

        return Task.FromResult<Returning<byte[]>>(Encoding.UTF8.GetBytes(text.ToString()));
    }
}
