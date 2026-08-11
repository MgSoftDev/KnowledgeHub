using System.Net.Http.Json;
using MgSoftDev.KnowledgeHub.Contracts;
using MgSoftDev.KnowledgeHub.Dtos;
using MgSoftDev.KnowledgeHub.Transport;
using MgSoftDev.ReturningCore;
using MgSoftDev.ReturningCore.Helper;

namespace MgSoftDev.KnowledgeHub.Http.Client;

/// <summary>
/// Export over HTTP, for clients with no store of their own (WASM). All the filtering happens on
/// the server: this only carries the request and the bytes.
/// </summary>
public sealed class HttpKnowledgeHubPdfExportService : IKnowledgeHubPdfExportService
{
    private readonly KnowledgeHubApiClient _api;

    public HttpKnowledgeHubPdfExportService(KnowledgeHubApiClient api) => _api = api;

    public Task<Returning<PdfExportDocument>> BuildAsync(Guid rootPagePk, bool includeDescendants) =>
        Task.FromResult<Returning<PdfExportDocument>>(Returning.Unfinished(
            "Construir el documento sin renderizarlo solo está disponible en el servidor",
            UnfinishedInfo.NotifyType.Warning));

    public Task<Returning<PdfFileDto>> ExportAsync(Guid rootPagePk, bool includeDescendants) =>
        Returning<PdfFileDto>.TryTask(async () =>
        {
            var url = $"{_api.BasePath}/pages/{rootPagePk}/pdf?descendants={includeDescendants.ToString().ToLowerInvariant()}";
            var response = await _api.Http.GetAsync(url);
            response.EnsureSuccessStatusCode();

            // The endpoint answers 200 either way; the content type says which. JSON means the
            // server rejected it for a business reason and the payload carries the explanation.
            var mediaType = response.Content.Headers.ContentType?.MediaType;
            if (string.Equals(mediaType, "application/json", StringComparison.OrdinalIgnoreCase))
            {
                var api = await response.Content.ReadFromJsonAsync<ApiResult<PdfFileDto>>();
                return api.ToReturning();
            }

            // Typed explicitly: mixing a bare PdfFileDto with the Returning<PdfFileDto> above makes
            // the TryTask overload ambiguous.
            Returning<PdfFileDto> file = new PdfFileDto
            {
                FileName = ReadFileName(response) ?? "documento.pdf",
                ContentType = mediaType ?? "application/pdf",
                Content = await response.Content.ReadAsByteArrayAsync()
            };
            return file;
        }, saveLog: true);

    private static string? ReadFileName(HttpResponseMessage response)
    {
        var name = response.Content.Headers.ContentDisposition?.FileNameStar
                   ?? response.Content.Headers.ContentDisposition?.FileName;
        return string.IsNullOrWhiteSpace(name) ? null : name.Trim('"');
    }
}
