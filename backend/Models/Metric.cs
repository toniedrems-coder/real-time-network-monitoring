namespace backend.Models;

public sealed record Metric(
    Guid Id,
    Guid EndpointId,
    DateTimeOffset Timestamp,
    double LatencyMs,
    double AvailabilityPercent,
    double ErrorRatePercent,
    double ThroughputMbps);