using backend.Agents.Rca.Models;
using backend.Models;
using backend.Services;

namespace backend.Agents.Rca.Tools;

public sealed class MetricsInvestigationTool
    : IInvestigationTool
{
    private const int RecentEndpointMetricsCount = 10;
    private const int RecentKpiSnapshotsCount = 6;

    private readonly KpiHistoryStore kpiHistoryStore;
    private readonly MetricsStore metricsStore;
    private readonly ILogger<MetricsInvestigationTool> logger;

    public MetricsInvestigationTool(
        KpiHistoryStore kpiHistoryStore,
        MetricsStore metricsStore,
        ILogger<MetricsInvestigationTool> logger)
    {
        this.kpiHistoryStore = kpiHistoryStore;
        this.metricsStore = metricsStore;
        this.logger = logger;
    }

    public string Name => "Metrics Investigation Tool";

    public string Description =>
        "Analyzes historical endpoint metrics and system-wide KPI " +
        "snapshots associated with an operational incident.";

    public bool CanInvestigate(Incident incident)
    {
        return incident.SourceId.HasValue;
    }

    public Task<IReadOnlyCollection<RcaEvidence>>
        InvestigateAsync(
            Incident incident,
            CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        logger.LogInformation(
            "Collecting historical metric evidence for incident " +
            "{IncidentNumber}, endpoint {EndpointId}.",
            incident.IncidentNumber,
            incident.SourceId);

        var evidence =
            new List<RcaEvidence>();

        if (!incident.SourceId.HasValue)
        {
            return Task.FromResult<
                IReadOnlyCollection<RcaEvidence>>(evidence);
        }

        CollectEndpointHistory(
            incident,
            evidence);

        CollectSystemKpiHistory(
            evidence);

        return Task.FromResult<
            IReadOnlyCollection<RcaEvidence>>(evidence);
    }

    // =========================================================
    // ENDPOINT METRIC HISTORY
    // =========================================================

    private void CollectEndpointHistory(
        Incident incident,
        List<RcaEvidence> evidence)
    {
        var endpointId =
            incident.SourceId!.Value;

        var history =
            metricsStore
                .GetHistory(endpointId)
                .OrderByDescending(x => x.Timestamp)
                .Take(RecentEndpointMetricsCount)
                .OrderBy(x => x.Timestamp)
                .ToList();

        if (history.Count == 0)
        {
            evidence.Add(
                new RcaEvidence
                {
                    Source = Name,
                    Type = "EndpointMetricHistory",
                    Description =
                        "No historical endpoint metrics were available.",
                    Value =
                        $"EndpointId={endpointId}"
                });

            return;
        }

        // -----------------------------------------------------
        // Sample count
        // -----------------------------------------------------

        evidence.Add(
            new RcaEvidence
            {
                Source = Name,
                Type = "EndpointMetricSampleCount",
                Description =
                    "Number of recent endpoint metric samples analyzed.",
                Value =
                    history.Count.ToString()
            });

        // -----------------------------------------------------
        // Average latency
        // -----------------------------------------------------

        var averageLatency =
            history.Average(x => x.LatencyMs);

        evidence.Add(
            new RcaEvidence
            {
                Source = Name,
                Type = "AverageLatency",
                Description =
                    "Average latency across recent endpoint samples.",
                Value =
                    $"{averageLatency:F2} ms"
            });

        // -----------------------------------------------------
        // Maximum latency
        // -----------------------------------------------------

        var maximumLatency =
            history.Max(x => x.LatencyMs);

        evidence.Add(
            new RcaEvidence
            {
                Source = Name,
                Type = "MaximumLatency",
                Description =
                    "Maximum latency observed across recent endpoint samples.",
                Value =
                    $"{maximumLatency:F2} ms"
            });

        // -----------------------------------------------------
        // Reachability history
        // -----------------------------------------------------

        var unreachableCount =
            history.Count(x => !x.IsReachable);

        evidence.Add(
            new RcaEvidence
            {
                Source = Name,
                Type = "UnreachableSamples",
                Description =
                    "Number of recent metric samples where the endpoint " +
                    "was unreachable.",
                Value =
                    $"{unreachableCount}/{history.Count}"
            });

        var reachableCount =
            history.Count(x => x.IsReachable);

        evidence.Add(
            new RcaEvidence
            {
                Source = Name,
                Type = "ReachableSamples",
                Description =
                    "Number of recent metric samples where the endpoint " +
                    "was reachable.",
                Value =
                    $"{reachableCount}/{history.Count}"
            });

        // -----------------------------------------------------
        // Availability
        // -----------------------------------------------------

        var averageAvailability =
            history.Average(
                x => x.AvailabilityPercent);

        evidence.Add(
            new RcaEvidence
            {
                Source = Name,
                Type = "AverageAvailability",
                Description =
                    "Average endpoint availability across recent samples.",
                Value =
                    $"{averageAvailability:F2}%"
            });

        // -----------------------------------------------------
        // Packet loss
        // -----------------------------------------------------

        var averagePacketLoss =
            history.Average(
                x => x.PacketLossPercent);

        evidence.Add(
            new RcaEvidence
            {
                Source = Name,
                Type = "AveragePacketLoss",
                Description =
                    "Average packet loss across recent endpoint samples.",
                Value =
                    $"{averagePacketLoss:F2}%"
            });

        // -----------------------------------------------------
        // Error rate
        // -----------------------------------------------------

        var averageErrorRate =
            history.Average(
                x => x.ErrorRatePercent);

        evidence.Add(
            new RcaEvidence
            {
                Source = Name,
                Type = "AverageErrorRate",
                Description =
                        "Average error rate across recent endpoint samples.",
                Value =
                        $"{averageErrorRate:F2}%"
            });

        // -----------------------------------------------------
        // Throughput
        // -----------------------------------------------------

        var averageThroughput =
            history.Average(
                x => x.ThroughputMbps);

        evidence.Add(
            new RcaEvidence
            {
                Source = Name,
                Type = "AverageThroughput",
                Description =
                    "Average throughput across recent endpoint samples.",
                Value =
                    $"{averageThroughput:F2} Mbps"
            });

        // -----------------------------------------------------
        // Latest metric
        // -----------------------------------------------------

        var latest =
            history.Last();

        evidence.Add(
            new RcaEvidence
            {
                Source = Name,
                Type = "LatestMetric",
                Description =
                    "Most recent endpoint metric state.",
                Value =
                    $"Reachable={latest.IsReachable}, " +
                    $"Latency={latest.LatencyMs:F2} ms, " +
                    $"Availability={latest.AvailabilityPercent:F2}%, " +
                    $"PacketLoss={latest.PacketLossPercent:F2}%, " +
                    $"ErrorRate={latest.ErrorRatePercent:F2}%, " +
                    $"Throughput={latest.ThroughputMbps:F2} Mbps, " +
                    $"Timestamp={latest.Timestamp:O}"
            });

        // -----------------------------------------------------
        // Consecutive failures
        // -----------------------------------------------------

        var consecutiveFailures =
            history
                .OrderByDescending(x => x.Timestamp)
                .TakeWhile(x => !x.IsReachable)
                .Count();

        evidence.Add(
            new RcaEvidence
            {
                Source = Name,
                Type = "ConsecutiveFailures",
                Description =
                    "Number of consecutive unreachable endpoint samples " +
                    "at the end of the metric window.",
                Value =
                    consecutiveFailures.ToString()
            });

        logger.LogInformation(
            "Metrics RCA analyzed {SampleCount} samples for endpoint " +
            "{EndpointId}. Average latency {AverageLatency:F2} ms, " +
            "{FailureCount} unreachable samples, " +
            "{ConsecutiveFailures} consecutive failures.",
            history.Count,
            endpointId,
            averageLatency,
            unreachableCount,
            consecutiveFailures);
    }

    // =========================================================
    // SYSTEM-WIDE KPI HISTORY
    // =========================================================

    private void CollectSystemKpiHistory(
        List<RcaEvidence> evidence)
    {
        var snapshots =
    kpiHistoryStore
        .GetAll()
        .Where(x => x.TotalEndpoints > 0)
        .OrderByDescending(x => x.Timestamp)
        .Take(RecentKpiSnapshotsCount)
        .OrderBy(x => x.Timestamp)
        .ToList();

        if (snapshots.Count == 0)
        {
            evidence.Add(
                new RcaEvidence
                {
                    Source = Name,
                    Type = "SystemKpiHistory",
                    Description =
                        "No valid system-wide KPI snapshots with monitored " +
                        "endpoints were available.",
                    Value =
                        "Platform health unavailable"
                });

            return;
        }

        var latest =
            snapshots.Last();

        // -----------------------------------------------------
        // Latest system health
        // -----------------------------------------------------

        evidence.Add(
            new RcaEvidence
            {
                Source = Name,
                Type = "SystemHealth",
                Description =
                    "Latest system-wide monitoring KPI snapshot.",
                Value =
                    $"Healthy={latest.HealthyEndpoints}/" +
                    $"{latest.TotalEndpoints}, " +
                    $"HealthyPercent={latest.HealthyPercent:F2}%, " +
                    $"AverageLatency={latest.AverageLatencyMs:F2} ms, " +
                    $"P95Latency={latest.P95LatencyMs:F2} ms"
            });

        // -----------------------------------------------------
        // Active anomalies
        // -----------------------------------------------------

        evidence.Add(
            new RcaEvidence
            {
                Source = Name,
                Type = "ActiveAnomalies",
                Description =
                    "Active anomalies from the latest KPI snapshot.",
                Value =
                    latest.ActiveAnomalies.ToString()
            });

        // -----------------------------------------------------
        // Endpoints at risk
        // -----------------------------------------------------

        evidence.Add(
            new RcaEvidence
            {
                Source = Name,
                Type = "EndpointsAtRisk",
                Description =
                    "Number of endpoints currently classified as at risk.",
                Value =
                    latest.EndpointsAtRisk.ToString()
            });

        // -----------------------------------------------------
        // Platform latency history
        // -----------------------------------------------------

        var averagePlatformLatency =
            snapshots.Average(
                x => x.AverageLatencyMs);

        evidence.Add(
            new RcaEvidence
            {
                Source = Name,
                Type = "PlatformAverageLatency",
                Description =
                    "Average platform latency across the recent " +
                    "KPI snapshot window.",
                Value =
                    $"{averagePlatformLatency:F2} ms"
            });

        logger.LogInformation(
            "System KPI RCA analyzed {SnapshotCount} snapshots. " +
            "Current healthy endpoints: {HealthyEndpoints}/{TotalEndpoints}. " +
            "Active anomalies: {ActiveAnomalies}. " +
            "Endpoints at risk: {EndpointsAtRisk}.",
            snapshots.Count,
            latest.HealthyEndpoints,
            latest.TotalEndpoints,
            latest.ActiveAnomalies,
            latest.EndpointsAtRisk);
    }
}