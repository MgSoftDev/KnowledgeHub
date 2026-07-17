using MgSoftDev.KnowledgeHub.Dtos;

namespace MgSoftDev.KnowledgeHub.Contracts;

/// <summary>
/// In-memory collector of navigation performance metrics, read by the diagnostics panel.
/// Registered scoped: per app in WPF/WASM, per circuit in Blazor Server. Operations are
/// pure in-memory bookkeeping and cannot fail, so they do not use the Returning pattern.
/// </summary>
public interface IKnowledgeHubDiagnostics
{
    DiagnosticsSnapshot? Last { get; }
    long CumulativeHits { get; }
    long CumulativeMisses { get; }

    /// <summary>Stores a navigation's metrics, folding its hits/misses into the cumulative totals.</summary>
    void Record(DiagnosticsSnapshot snapshot);

    void ResetCumulative();
}
