namespace backend.Models;

public sealed record Anomaly(
    Guid Id,
    Guid EndpointId,
    Guid MetricId,
    string Type,
    string Severity,
    DateTimeOffset DetectedAt);