using System.Diagnostics;
using MgSoftDev.KnowledgeHub.Blazor.Helpers;
using MgSoftDev.KnowledgeHub.Contracts;
using MgSoftDev.KnowledgeHub.Dtos;
using MgSoftDev.PrismPlus.Returning.Commands;
using MgSoftDev.ReturningCore;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Radzen;

namespace MgSoftDev.KnowledgeHub.Blazor.Components.Embedded;

/// <summary>Performance/cache diagnostics panel. Embeddable anywhere.</summary>
public partial class KnowledgeHubDiagnosticsPanel : ComponentBase
{
    [Inject] private IKnowledgeHubDiagnostics DiagnosticsService { get; set; } = null!;
    [Inject] private IKnowledgeHubHtmlImageRewriter Rewriter { get; set; } = null!;
    [Inject] private IKnowledgeHubPageService DocService { get; set; } = null!;
    [Inject] private IServiceProvider Services { get; set; } = null!;
    [Inject] private NotificationService Notify { get; set; } = null!;

    /// <summary>Optional: absent in hosts without a local disk cache (e.g. WASM clients).</summary>
    protected IKnowledgeHubImageCache? Cache { get; private set; }

    protected DiagnosticsSnapshot? Last => DiagnosticsService.Last;
    protected long CumulativeHits => DiagnosticsService.CumulativeHits;
    protected long CumulativeMisses => DiagnosticsService.CumulativeMisses;
    protected double CacheSizeKb => (Cache?.GetCacheFolderSizeBytes() ?? 0) / 1024.0;

    public bool Wait { get; private set; }
    protected SimSummary? SimResult { get; private set; }

    protected sealed record SimSummary(int Count, double AvgMs, double P95Ms, int Hits, int Misses);

    protected override void OnInitialized() => Cache = Services.GetService<IKnowledgeHubImageCache>();

    private void Refresh() => StateHasChanged();

    public ReturningCommand ClearCacheCommand =>
        field ??= new ReturningCommand(() =>
        {
            if (Cache is null)
                return Returning.Unfinished("Este anfitrión no tiene caché local", UnfinishedInfo.NotifyType.Warning);

            var result = Cache.ClearCache();
            if (!result.Ok) return result;

            Notify.ShowSuccess("Caché limpiado (cold start)");
            return Returning.Success();
        }, () => !Wait)
        .StartAction(() => Wait = true)
        .EndAction(r =>
        {
            Wait = false;
            r.SendNotifyIfNotOk(Notify, "Error al limpiar el caché");
            StateHasChanged();
        });

    public AsyncReturningCommand SimulateCommand =>
        field ??= new AsyncReturningCommand(async () =>
        {
            var tree = await DocService.GetTreeAsync();
            if (!tree.OkNotNull)
                return Returning.Unfinished("No se pudo cargar el árbol", UnfinishedInfo.NotifyType.Warning);

            var pks = Flatten(tree.Value).ToList();
            if (pks.Count == 0)
                return Returning.Unfinished("No hay páginas visibles para simular", UnfinishedInfo.NotifyType.Warning);

            var times = new List<double>();
            var hits = 0;
            var misses = 0;

            for (var i = 0; i < 50; i++)
            {
                var pk = pks[Random.Shared.Next(pks.Count)];
                var sw = Stopwatch.StartNew();

                var read = await DocService.GetPageForReadAsync(pk);
                if (read.OkNotNull)
                {
                    var rewrite = await Rewriter.PrepareForDisplayAsync(read.Value.ContentHtml);
                    if (rewrite.OkNotNull)
                    {
                        hits += rewrite.Value.CacheHits;
                        misses += rewrite.Value.CacheMisses;
                    }
                }

                sw.Stop();
                times.Add(sw.Elapsed.TotalMilliseconds);
            }

            times.Sort();
            var avg = times.Average();
            var p95 = times[(int)Math.Ceiling(0.95 * times.Count) - 1];
            SimResult = new SimSummary(times.Count, avg, p95, hits, misses);
            return Returning.Success();
        }, () => !Wait)
        .StartAction(() => Wait = true)
        .EndAction(r =>
        {
            Wait = false;
            r.SendNotifyIfNotOk(Notify, "Error durante la simulación");
            StateHasChanged();
        });

    private static IEnumerable<Guid> Flatten(IEnumerable<PageTreeNodeDto> nodes)
    {
        foreach (var node in nodes)
        {
            if (node.HasPublishedVersion) yield return node.Pk;
            foreach (var child in Flatten(node.Children))
                yield return child;
        }
    }
}
