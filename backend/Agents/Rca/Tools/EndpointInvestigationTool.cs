using backend.Agents.Rca.Models;
using backend.Agents.Tools.Monitoring;
using backend.Models;
using backend.Services;

namespace backend.Agents.Rca.Tools;

public sealed class EndpointInvestigationTool
    : IInvestigationTool
{
    private readonly EndpointService endpointService;
    private readonly IEndpointProbeService endpointProbeService;
    private readonly ILogger<EndpointInvestigationTool> logger;

    public EndpointInvestigationTool(
        EndpointService endpointService,
        IEndpointProbeService endpointProbeService,
        ILogger<EndpointInvestigationTool> logger)
    {
        this.endpointService = endpointService;
        this.endpointProbeService = endpointProbeService;
        this.logger = logger;
    }

    public string Name => "Endpoint Investigation Tool";

    public string Description =>
        "Re-probes an affected endpoint and collects current " +
        "availability, HTTP status and latency evidence.";

    public bool CanInvestigate(Incident incident)
    {
        return incident.TargetType.Equals(
            "Endpoint",
            StringComparison.OrdinalIgnoreCase)
            && incident.SourceId.HasValue;
    }

    public async Task<IReadOnlyCollection<RcaEvidence>>
        InvestigateAsync(
            Incident incident,
            CancellationToken cancellationToken = default)
    {
        var evidence = new List<RcaEvidence>();

        if (!incident.SourceId.HasValue)
            return evidence;

        var endpoint = endpointService
            .GetAll()
            .FirstOrDefault(
                x => x.Id == incident.SourceId.Value);

        if (endpoint is null)
        {
            evidence.Add(
                new RcaEvidence
                {
                    Source = Name,
                    Type = "EndpointLookup",
                    Description =
                        "The monitored endpoint could not be found " +
                        "in the current endpoint registry.",
                    Value = incident.Target
                });

            return evidence;
        }

        logger.LogInformation(
            "RCA re-probing endpoint {EndpointUrl} for incident {IncidentNumber}.",
            endpoint.Url,
            incident.IncidentNumber);

        var result =
            await endpointProbeService.ProbeAsync(
                endpoint,
                cancellationToken);

        evidence.Add(
            new RcaEvidence
            {
                Source = Name,
                Type = "Reachability",
                Description =
                    "Current endpoint reachability.",
                Value =
                    result.Reachable
                        ? "Reachable"
                        : "Unreachable"
            });

        evidence.Add(
            new RcaEvidence
            {
                Source = Name,
                Type = "Latency",
                Description =
                    "Current endpoint response latency.",
                Value =
                    $"{result.LatencyMs:F2} ms"
            });

        evidence.Add(
            new RcaEvidence
            {
                Source = Name,
                Type = "Status",
                Description =
                    "Current endpoint probe status.",
                Value = result.Status
            });

        if (result.StatusCode.HasValue)
        {
            evidence.Add(
                new RcaEvidence
                {
                    Source = Name,
                    Type = "HttpStatusCode",
                    Description =
                        "Current HTTP response status code.",
                    Value =
                        result.StatusCode.Value.ToString()
                });
        }

        if (!string.IsNullOrWhiteSpace(
            result.ErrorMessage))
        {
            evidence.Add(
                new RcaEvidence
                {
                    Source = Name,
                    Type = "ProbeError",
                    Description =
                        "Error returned by the RCA endpoint probe.",
                    Value =
                        result.ErrorMessage
                });
        }

        return evidence;
    }
}