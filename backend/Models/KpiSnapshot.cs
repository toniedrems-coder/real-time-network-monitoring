namespace backend.Models;

public sealed partial record KpiSnapshot(
    DateTimeOffset Timestamp,
    int TotalEndpoints,
    int HealthyEndpoints,
    double HealthyPercent,
    double AverageLatencyMs,
    double P95LatencyMs,
    int ActiveAnomalies,
    double AnomalyRatePerHour,
    int EndpointsAtRisk)
{
    public Guid Id { get; init; } = Guid.NewGuid();
}