using MgSoftDev.KnowledgeHub.Contracts;
using MgSoftDev.KnowledgeHub.Dtos;

namespace MgSoftDev.KnowledgeHub.Diagnostics;

/// <summary>
/// Thread-safe, dependency-free diagnostics collector. Lives in Abstractions so every
/// hosting model (including WASM clients that never load the core package) can register it.
/// </summary>
public sealed class InMemoryKnowledgeHubDiagnostics : IKnowledgeHubDiagnostics
{
    private readonly Lock _gate = new();

    private DiagnosticsSnapshot? _last;
    private long _cumulativeHits;
    private long _cumulativeMisses;

    public DiagnosticsSnapshot? Last
    {
        get { lock (_gate) return _last; }
    }

    public long CumulativeHits
    {
        get { lock (_gate) return _cumulativeHits; }
    }

    public long CumulativeMisses
    {
        get { lock (_gate) return _cumulativeMisses; }
    }

    public void Record(DiagnosticsSnapshot snapshot)
    {
        lock (_gate)
        {
            _cumulativeHits += snapshot.CacheHits;
            _cumulativeMisses += snapshot.CacheMisses;
            snapshot.CumulativeHits = _cumulativeHits;
            snapshot.CumulativeMisses = _cumulativeMisses;
            _last = snapshot;
        }
    }

    public void ResetCumulative()
    {
        lock (_gate)
        {
            _cumulativeHits = 0;
            _cumulativeMisses = 0;
            if (_last is not null)
            {
                _last.CumulativeHits = 0;
                _last.CumulativeMisses = 0;
            }
        }
    }
}
