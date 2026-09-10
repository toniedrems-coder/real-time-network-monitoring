namespace backend.Models;

/// <summary>
/// A point-in-time snapshot of system-wide KPIs, recorded periodically by
/// <see cref="backend.Services.KpiSnapshotWorker"/> so the dashboard can render
/// historical trend charts in addition to the current live values.
/// </summary>
public sealed record KpiSnapshot(
    DateTimeOffset Timestamp,
    int TotalEndpoints,
    int HealthyEndpoints,
    double HealthyPercent,
    double AverageLatencyMs,
    double P95LatencyMs,
    int ActiveAnomalies,
    double AnomalyRatePerHour,
    int EndpointsAtRisk);
