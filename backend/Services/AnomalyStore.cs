using System.Collections.Concurrent;
using backend.Models;

namespace backend.Services;

/// <summary>
/// Shared in-memory store of detected anomalies per endpoint, populated by
/// <see cref="AnomalyIngestionWorker"/> as anomalies arrive from Kafka
/// (<c>endpoint.anomalies</c> topic). Read by the API layer (controllers, reporting).
/// </summary>
public sealed class AnomalyStore
{
    private const int MaxAnomaliesPerEndpoint = 100;

    private readonly ConcurrentDictionary<Guid, ConcurrentQueue<DetectedAnomaly>> anomaliesByEndpoint = new();

    public void Record(DetectedAnomaly anomaly)
    {
        var queue = anomaliesByEndpoint.GetOrAdd(anomaly.EndpointId, _ => new ConcurrentQueue<DetectedAnomaly>());
        queue.Enqueue(anomaly);
        while (queue.Count > MaxAnomaliesPerEndpoint)
        {
            queue.TryDequeue(out _);
        }
    }

    public IReadOnlyList<DetectedAnomaly> GetForEndpoint(Guid endpointId)
    {
        return anomaliesByEndpoint.TryGetValue(endpointId, out var queue)
            ? queue.ToArray()
            : [];
    }

    public IReadOnlyList<DetectedAnomaly> GetAll()
    {
        return anomaliesByEndpoint.Values
            .SelectMany(queue => queue)
            .OrderByDescending(anomaly => anomaly.DetectedAt)
            .ToArray();
    }
}
