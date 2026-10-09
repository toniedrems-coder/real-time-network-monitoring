using backend.Agents.Remediation.Models;
using backend.Models;

namespace backend.Agents.Remediation.Services;

public sealed class RemediationRecommendationService
{
    private readonly ILogger<RemediationRecommendationService>
        _logger;

    public RemediationRecommendationService(
        ILogger<RemediationRecommendationService> logger)
    {
        _logger = logger;
    }

    public RemediationRecommendation Recommend(
        Incident incident)
    {
        _logger.LogInformation(
            "Generating remediation recommendation for incident {IncidentNumber}. FailureType: {FailureType}.",
            incident.IncidentNumber,
            incident.FailureType);

        /*
         * Phase 1 rule:
         *
         * Endpoint failures are first handled using the safest
         * available remediation: recheck the endpoint.
         *
         * We deliberately do NOT jump directly from a timeout
         * to RestartPod/ScaleDeployment because an external
         * endpoint failure does not prove Kubernetes is the cause.
         */
        if (IsEndpointIncident(incident))
        {
            return new RemediationRecommendation
            {
                ActionType =
                    RemediationActionType.RecheckEndpoint,

                RunbookName =
                    "endpoint-recheck",

                Reason =
                    BuildEndpointReason(incident),

                Confidence =
                    0.90,

                Source =
                    "Deterministic Remediation Rule"
            };
        }

        /*
         * Safe fallback.
         *
         * At this stage we should not invent infrastructure
         * operations for unsupported target types.
         */
        return new RemediationRecommendation
        {
            ActionType =
                RemediationActionType.ExecuteRunbook,

            RunbookName =
                "manual-investigation",

            Reason =
                $"No safe automatic remediation rule exists for target type '{incident.TargetType}'. Manual investigation is required.",

            Confidence =
                0.50,

            Source =
                "Default Safe Remediation Rule"
        };
    }

    private static bool IsEndpointIncident(
        Incident incident)
    {
        return string.Equals(
            incident.TargetType,
            "Endpoint",
            StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildEndpointReason(
        Incident incident)
    {
        var rootCause =
            string.IsNullOrWhiteSpace(incident.RootCause)
                ? "Endpoint health requires verification."
                : incident.RootCause;

        return
            $"Recheck endpoint connectivity before attempting a more invasive remediation. RCA: {rootCause}";
    }
}