namespace backend.Models;

public sealed record DashboardEndpointSummary(
    Guid EndpointId,
    string Name,
    string Url,
    string Status,
    double LatencyMs,
    double AvailabilityPercent,
    int AnomalyCount);

public sealed record DashboardView(
    DateTimeOffset GeneratedAt,
    int TotalEndpoints,
    int HealthyEndpoints,
    int ActiveAnomalies,
    IReadOnlyList<DashboardEndpointSummary> Endpoints);
