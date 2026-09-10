namespace backend.Models;

public sealed record DashboardEndpointSummary(
    Guid EndpointId,
    string Name,
    string Url,
    string Status,
    double LatencyMs,
    double AvailabilityPercent,
    int AnomalyCount,
    DateTimeOffset? LastAnomalyAt);

public sealed record DashboardView(
    DateTimeOffset GeneratedAt,
    int TotalEndpoints,
    int HealthyEndpoints,
    int ActiveAnomalies,
    double HealthyPercent,
    double AverageLatencyMs,
    double P95LatencyMs,
    double AnomalyRatePerHour,
    int EndpointsAtRisk,
    IReadOnlyList<DashboardEndpointSummary> Endpoints,
    IReadOnlyList<KpiSnapshot> KpiHistory);
