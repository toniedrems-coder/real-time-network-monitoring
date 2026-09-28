using backend.Agents.Rca.Models;
using backend.Models;

namespace backend.Services;

public sealed class RootCauseAnalysisService
{
    public RootCauseAnalysis Analyze(
        Incident incident,
        IReadOnlyCollection<RcaEvidence> evidence)
    {
        var probableCause =
            DetermineProbableCause(
                incident,
                evidence);

        var confidence =
            CalculateConfidence(
                incident,
                evidence);

        return new RootCauseAnalysis
        {
            IncidentId = incident.Id,
            IncidentNumber = incident.IncidentNumber,
            Target = incident.Target,

            ProbableCause = probableCause,

            Confidence = confidence,

            ConfidenceLevel =
                GetConfidenceLevel(confidence),

            Evidence = evidence.ToList(),

            RecommendedActions =
                DetermineRecommendedActions(
                    incident,
                    evidence),

            RequiresEscalation =
                ShouldEscalate(
                    incident,
                    evidence,
                    confidence)
        };
    }

    // =========================================================
    // ROOT CAUSE CORRELATION
    // =========================================================

    private static string DetermineProbableCause(
        Incident incident,
        IReadOnlyCollection<RcaEvidence> evidence)
    {
        var currentReachability =
            GetEvidenceValue(
                evidence,
                "Endpoint Investigation Tool",
                "Reachability");

        var consecutiveFailures =
            GetIntegerEvidence(
                evidence,
                "Metrics Investigation Tool",
                "ConsecutiveFailures");

        var unreachableSamples =
            GetRatioEvidence(
                evidence,
                "Metrics Investigation Tool",
                "UnreachableSamples");

        var systemHealth =
            GetEvidenceValue(
                evidence,
                "Metrics Investigation Tool",
                "SystemHealth");

        var platformHealthy =
            IsPlatformGenerallyHealthy(
                systemHealth);

        // -----------------------------------------------------
        // CASE 1
        // Original incident failed but live RCA probe recovered.
        // -----------------------------------------------------

        if (IsReachable(currentReachability) &&
            incident.FailureType is
                FailureType.Timeout or
                FailureType.Unavailable)
        {
            if (consecutiveFailures == 0 &&
                platformHealthy == true)
            {
                return
                    "The endpoint timed out or became unavailable when " +
                    "the incident was detected, but the live RCA probe " +
                    "is now successful. Recent endpoint metrics show no " +
                    "continuing failure and the wider monitoring platform " +
                    "appears healthy. The evidence is most consistent " +
                    "with a transient endpoint, network, DNS, upstream " +
                    "dependency, or application responsiveness issue.";
            }

            if (unreachableSamples is not null &&
                unreachableSamples.Value.Failed > 0)
            {
                return
                    "The endpoint is currently reachable, but historical " +
                    "metrics contain failed samples. This suggests an " +
                    "intermittent rather than continuously active failure. " +
                    "Possible causes include unstable network connectivity, " +
                    "intermittent application latency, DNS behaviour, or " +
                    "an unreliable upstream dependency.";
            }

            return
                "The endpoint was unavailable or timed out when the " +
                "incident was detected, but the RCA verification probe " +
                "is now successful. The available evidence suggests a " +
                "transient condition that has recovered.";
        }

        // -----------------------------------------------------
        // CASE 2
        // Current endpoint failure plus broader platform
        // degradation.
        //
        // IMPORTANT:
        // Evaluate this BEFORE the localized persistent-failure
        // rule because an unhealthy platform changes the scope
        // of the RCA.
        // -----------------------------------------------------

        if (IsUnreachable(currentReachability) &&
            platformHealthy == false)
        {
            return
                "The endpoint remains unreachable and the latest system " +
                "KPIs indicate broader monitoring degradation. The target " +
                "endpoint has repeated consecutive failures, while overall " +
                "platform health is also degraded. The issue may therefore " +
                "involve shared infrastructure, network connectivity, " +
                "platform services, ingress or load-balancing components, " +
                "or common upstream dependencies rather than being isolated " +
                "to the target endpoint.";
        }

        // -----------------------------------------------------
        // CASE 3
        // Live probe is still failing and historical failures
        // are consecutive, but the wider platform is healthy.
        // -----------------------------------------------------

        if (IsUnreachable(currentReachability) &&
            consecutiveFailures is >= 2)
        {
            if (platformHealthy == true)
            {
                return
                    "The endpoint remains unreachable during RCA and " +
                    "historical metrics show consecutive failures. Other " +
                    "platform-level KPIs appear generally healthy, which " +
                    "suggests the failure is localized to this endpoint, " +
                    "its application, network path, DNS configuration, " +
                    "or one of its upstream dependencies.";
            }

            return
                "The endpoint remains unreachable during RCA and recent " +
                "metrics show consecutive failures. The evidence indicates " +
                "a persistent operational issue rather than a single " +
                "transient monitoring failure.";
        }
        // -----------------------------------------------------
        // CASE 4
        // Significant failures exist historically even if we
        // cannot establish a live reachability state.
        // -----------------------------------------------------

        if (unreachableSamples is not null &&
            unreachableSamples.Value.Total > 0)
        {
            var failureRate =
                (double)unreachableSamples.Value.Failed /
                unreachableSamples.Value.Total;

            if (failureRate >= 0.50)
            {
                return
                    $"Historical endpoint metrics show " +
                    $"{unreachableSamples.Value.Failed} failed samples " +
                    $"out of {unreachableSamples.Value.Total} recent " +
                    $"samples. The high failure ratio indicates a " +
                    $"recurring or persistent availability problem " +
                    $"requiring further application, network and " +
                    $"dependency investigation.";
            }
        }

        // -----------------------------------------------------
        // FALLBACK
        // Use the original incident classification when richer
        // evidence is insufficient.
        // -----------------------------------------------------

        return incident.FailureType switch
        {
            FailureType.Timeout =>
                "The endpoint did not respond within the configured " +
                "monitoring timeout. Possible causes include application " +
                "latency, network connectivity, DNS resolution, upstream " +
                "dependency delays, or service saturation.",

            FailureType.Unavailable =>
                "The monitoring platform could not establish a successful " +
                "connection to the endpoint.",

            FailureType.HttpError when
                incident.HttpStatusCode is >= 500 =>
                "The endpoint returned a server-side HTTP error, " +
                "indicating a possible application or upstream service failure.",

            FailureType.HttpError when
                incident.HttpStatusCode is >= 400 =>
                "The endpoint returned a client-side HTTP error.",

            FailureType.Anomaly =>
                "Monitoring metrics deviated from the expected operating pattern.",

            _ =>
                "The available evidence is insufficient to determine " +
                "a specific root cause."
        };
    }

    // =========================================================
    // CONFIDENCE
    // =========================================================

    private static double CalculateConfidence(
        Incident incident,
        IReadOnlyCollection<RcaEvidence> evidence)
    {
        var confidence = 0.30;

        if (incident.FailureType != FailureType.Unknown)
        {
            confidence += 0.15;
        }

        if (!string.IsNullOrWhiteSpace(
            incident.ErrorMessage))
        {
            confidence += 0.05;
        }

        if (incident.HttpStatusCode.HasValue)
        {
            confidence += 0.05;
        }

        // Live endpoint verification is strong RCA evidence.
        if (HasEvidence(
            evidence,
            "Endpoint Investigation Tool",
            "Reachability"))
        {
            confidence += 0.15;
        }

        // Historical endpoint metrics.
        if (HasEvidence(
            evidence,
            "Metrics Investigation Tool",
            "UnreachableSamples"))
        {
            confidence += 0.10;
        }

        if (HasEvidence(
            evidence,
            "Metrics Investigation Tool",
            "ConsecutiveFailures"))
        {
            confidence += 0.10;
        }

        // Platform context.
        if (HasEvidence(
            evidence,
            "Metrics Investigation Tool",
            "SystemHealth"))
        {
            confidence += 0.05;
        }

        return Math.Min(
            Math.Round(confidence, 2),
            1.0);
    }

    private static string GetConfidenceLevel(
        double confidence)
    {
        return confidence switch
        {
            >= 0.80 => "High",
            >= 0.50 => "Medium",
            _ => "Low"
        };
    }

    // =========================================================
    // RECOMMENDATIONS
    // =========================================================

    private static List<string> DetermineRecommendedActions(
        Incident incident,
        IReadOnlyCollection<RcaEvidence> evidence)
    {
        var currentReachability =
            GetEvidenceValue(
                evidence,
                "Endpoint Investigation Tool",
                "Reachability");

        var consecutiveFailures =
            GetIntegerEvidence(
                evidence,
                "Metrics Investigation Tool",
                "ConsecutiveFailures");

        var systemHealth =
            GetEvidenceValue(
                evidence,
                "Metrics Investigation Tool",
                "SystemHealth");

        var platformHealthy =
            IsPlatformGenerallyHealthy(
                systemHealth);

        // Endpoint recovered during RCA.
        if (IsReachable(currentReachability) &&
            incident.FailureType is
                FailureType.Timeout or
                FailureType.Unavailable)
        {
            return
            [
                "Continue monitoring the endpoint for recurrence.",
                "Review historical latency and availability around the incident time.",
                "Inspect application and infrastructure logs around the failure window.",
                "Check DNS, network and upstream dependency events around the incident time.",
                "Resolve the incident only after a verification period confirms stability."
            ];
        }

        // Persistent localized failure.
        if (IsUnreachable(currentReachability) &&
            consecutiveFailures is >= 2 &&
            platformHealthy == true)
        {
            return
            [
                "Inspect the affected application or service health.",
                "Inspect application logs for the affected endpoint.",
                "Verify DNS resolution for the target.",
                "Verify network connectivity to the target.",
                "Check upstream dependencies.",
                "Check recent deployments or configuration changes.",
                "Prepare an approved remediation runbook if the failure persists."
            ];
        }

        // Possible wider problem.
        if (IsUnreachable(currentReachability) &&
            platformHealthy == false)
        {
            return
            [
                "Investigate platform-wide monitoring health.",
                "Check shared infrastructure and network connectivity.",
                "Inspect other unhealthy endpoints for correlation.",
                "Check common upstream dependencies.",
                "Inspect Kubernetes, ingress and load-balancer health where applicable.",
                "Escalate if multiple production services are affected."
            ];
        }

        return incident.FailureType switch
        {
            FailureType.Timeout =>
            [
                "Retry the endpoint probe.",
                "Check DNS resolution.",
                "Check network connectivity.",
                "Inspect application health.",
                "Inspect application and infrastructure logs."
            ],

            FailureType.Unavailable =>
            [
                "Verify DNS resolution.",
                "Verify network connectivity.",
                "Check service availability.",
                "Inspect infrastructure health."
            ],

            FailureType.HttpError =>
            [
                "Inspect application logs.",
                "Check upstream dependencies.",
                "Check application health.",
                "Review recent deployments."
            ],

            _ =>
            [
                "Collect additional operational evidence.",
                "Inspect logs and metrics.",
                "Escalate if the issue persists."
            ]
        };
    }

    // =========================================================
    // ESCALATION
    // =========================================================

    private static bool ShouldEscalate(
        Incident incident,
        IReadOnlyCollection<RcaEvidence> evidence,
        double confidence)
    {
        if (incident.Severity == IncidentSeverity.P1)
        {
            return true;
        }

        if (confidence < 0.50)
        {
            return true;
        }

        var currentReachability =
            GetEvidenceValue(
                evidence,
                "Endpoint Investigation Tool",
                "Reachability");

        var consecutiveFailures =
            GetIntegerEvidence(
                evidence,
                "Metrics Investigation Tool",
                "ConsecutiveFailures");

        if (IsUnreachable(currentReachability) &&
            consecutiveFailures is >= 3)
        {
            return true;
        }

        return false;
    }

    // =========================================================
    // EVIDENCE HELPERS
    // =========================================================

    private static bool HasEvidence(
        IReadOnlyCollection<RcaEvidence> evidence,
        string source,
        string type)
    {
        return evidence.Any(
            x =>
                x.Source.Equals(
                    source,
                    StringComparison.OrdinalIgnoreCase)
                &&
                x.Type.Equals(
                    type,
                    StringComparison.OrdinalIgnoreCase));
    }

    private static string? GetEvidenceValue(
        IReadOnlyCollection<RcaEvidence> evidence,
        string source,
        string type)
    {
        return evidence
            .LastOrDefault(
                x =>
                    x.Source.Equals(
                        source,
                        StringComparison.OrdinalIgnoreCase)
                    &&
                    x.Type.Equals(
                        type,
                        StringComparison.OrdinalIgnoreCase))
            ?.Value;
    }

    private static int? GetIntegerEvidence(
        IReadOnlyCollection<RcaEvidence> evidence,
        string source,
        string type)
    {
        var value =
            GetEvidenceValue(
                evidence,
                source,
                type);

        return int.TryParse(
            value,
            out var result)
                ? result
                : null;
    }

    private static (int Failed, int Total)?
        GetRatioEvidence(
            IReadOnlyCollection<RcaEvidence> evidence,
            string source,
            string type)
    {
        var value =
            GetEvidenceValue(
                evidence,
                source,
                type);

        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var parts =
            value.Split(
                '/',
                StringSplitOptions.TrimEntries |
                StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length != 2)
        {
            return null;
        }

        if (!int.TryParse(
                parts[0],
                out var failed))
        {
            return null;
        }

        if (!int.TryParse(
                parts[1],
                out var total))
        {
            return null;
        }

        return (failed, total);
    }

    private static bool IsReachable(
        string? value)
    {
        return string.Equals(
            value,
            "Reachable",
            StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsUnreachable(
        string? value)
    {
        return string.Equals(
            value,
            "Unreachable",
            StringComparison.OrdinalIgnoreCase);
    }

    private static bool? IsPlatformGenerallyHealthy(
        string? systemHealth)
    {
        if (string.IsNullOrWhiteSpace(systemHealth))
        {
            return null;
        }

        // Expected format:
        //
        // Healthy=2/3,
        // HealthyPercent=66.67%,
        // AverageLatency=...
        //
        // A snapshot such as Healthy=0/0 does NOT contain enough
        // information to determine platform health.

        const string healthyMarker = "Healthy=";

        var healthyMarkerIndex =
            systemHealth.IndexOf(
                healthyMarker,
                StringComparison.OrdinalIgnoreCase);

        if (healthyMarkerIndex < 0)
        {
            return null;
        }

        var healthyStart =
            healthyMarkerIndex + healthyMarker.Length;

        var healthyEnd =
            systemHealth.IndexOf(
                ',',
                healthyStart);

        if (healthyEnd < 0)
        {
            healthyEnd = systemHealth.Length;
        }

        var healthyRatio =
            systemHealth[healthyStart..healthyEnd]
                .Trim();

        var ratioParts =
            healthyRatio.Split(
                '/',
                StringSplitOptions.TrimEntries |
                StringSplitOptions.RemoveEmptyEntries);

        if (ratioParts.Length != 2)
        {
            return null;
        }

        if (!int.TryParse(
                ratioParts[0],
                out var healthyEndpoints))
        {
            return null;
        }

        if (!int.TryParse(
                ratioParts[1],
                out var totalEndpoints))
        {
            return null;
        }

        // 0/0 means no platform population was measured.
        // Do not classify it as healthy or degraded.
        if (totalEndpoints <= 0)
        {
            return null;
        }

        var healthyPercent =
            (double)healthyEndpoints /
            totalEndpoints *
            100.0;

        // Initial V2 policy.
        // Later move this threshold into configuration.
        return healthyPercent >= 80.0;
    }

}