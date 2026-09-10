namespace backend.Models;

public sealed partial record EndpointMetrics(
    Guid EndpointId,
    DateTimeOffset Timestamp,
    double LatencyMs,
    double PacketLossPercent,
    double AvailabilityPercent,
    double ErrorRatePercent,
    double ThroughputMbps,
    bool IsReachable);

public partial record EndpointMetrics
{
    public Guid Id { get; init; } = Guid.NewGuid();
}

public sealed record MetricTrendPoint(
    DateTimeOffset Timestamp,
    double LatencyMs,
    double AvailabilityPercent,
    double ErrorRatePercent,
    double ThroughputMbps);
