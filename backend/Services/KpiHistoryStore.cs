using System.Collections.Concurrent;
using backend.Models;

namespace backend.Services;

/// <summary>
/// Shared in-memory rolling history of system-wide KPI snapshots, populated by
/// <see cref="KpiSnapshotWorker"/> on a fixed interval. Powers historical trend
/// charts (healthy %, latency, anomaly rate) on the dashboard independent of any
/// single endpoint's metric history.
/// </summary>
public sealed class KpiHistoryStore
{
    private const int MaxSnapshots = 288; // 24h of history at a 5-minute cadence

    private readonly ConcurrentQueue<KpiSnapshot> snapshots = new();

    public void Record(KpiSnapshot snapshot)
    {
        snapshots.Enqueue(snapshot);
        while (snapshots.Count > MaxSnapshots)
        {
            snapshots.TryDequeue(out _);
        }
    }

    public IReadOnlyList<KpiSnapshot> GetAll()
    {
        return snapshots.ToArray();
    }
}
