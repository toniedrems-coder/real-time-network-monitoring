namespace backend.Agents.Tools.Monitoring;

public sealed record EndpointProbeResult(
    Guid EndpointId,
    string Url,
    DateTimeOffset Timestamp,
    bool Reachable,
    int? StatusCode,
    double LatencyMs,
    double ThroughputMbps,
    string Status,
    string? ErrorMessage = null);