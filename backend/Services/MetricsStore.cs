using System.Collections.Concurrent;
using backend.Models;

namespace backend.Services;

/// <summary>
/// Shared in-memory store for the latest metrics and rolling history per endpoint.
/// Populated by the Kafka consumer (<see cref="MetricsIngestionWorker"/>) and read
/// by the API layer (controllers, <see cref="ReportingService"/>, <see cref="AnomalyDetectionService"/>).
/// </summary>
public sealed class MetricsStore
{
    private const int MaxHistoryPerEndpoint = 100;

    private readonly ConcurrentDictionary<Guid, EndpointMetrics> latestMetrics = new();
    private readonly ConcurrentDictionary<Guid, ConcurrentQueue<EndpointMetrics>> history = new();

    public EndpointMetrics? GetLatest(Guid endpointId)
    {
        return latestMetrics.TryGetValue(endpointId, out var metrics) ? metrics : null;
    }

    public IReadOnlyList<EndpointMetrics> GetHistory(Guid endpointId)
    {
        return history.TryGetValue(endpointId, out var metrics)
            ? metrics.ToArray()
            : [];
    }

    public void Record(EndpointMetrics metrics)
    {
        latestMetrics[metrics.EndpointId] = metrics;
        var endpointHistory = history.GetOrAdd(metrics.EndpointId, _ => new ConcurrentQueue<EndpointMetrics>());
        endpointHistory.Enqueue(metrics);
        while (endpointHistory.Count > MaxHistoryPerEndpoint)
        {
            endpointHistory.TryDequeue(out _);
        }
    }
}
