using backend.Agents.Abstractions;
using backend.Agents.Models;
using backend.Agents.Tools.Monitoring;
using backend.Models;
using backend.Services;

namespace backend.Agents.Monitoring;

public class MonitoringAgent : IAiOpsAgent
{
    private readonly ILogger<MonitoringAgent> _logger;
    private readonly EndpointService endpointService;
    private readonly IEndpointProbeService endpointProbeService;

    private readonly IServiceScopeFactory scopeFactory;



    public MonitoringAgent(
        ILogger<MonitoringAgent> logger,
        EndpointService endpointService,
        IEndpointProbeService endpointProbeService,
        IServiceScopeFactory scopeFactory)
    {
        _logger = logger;   
        this.endpointService = endpointService;
        this.endpointProbeService = endpointProbeService;   
        this.scopeFactory = scopeFactory;
    }

    public string Id => "monitoring-agent";

    public string Name => "Monitoring Agent";

    public string Description =>
        "Monitors endpoints, applications and infrastructure health.";

    public AgentStatus Status { get; private set; }
        = AgentStatus.Stopped;

    public bool Enabled => true;

    public async Task<AgentResult> ExecuteAsync(AgentContext context, CancellationToken cancellationToken = default)
    {
        var startedAt = DateTimeOffset.UtcNow;

        try
        {
            Status = AgentStatus.Running;

            _logger.LogInformation("Monitoring Agent started. ExecutionId: {ExecutionId}", context.ExecutionId);

            var endpoints = endpointService.GetAll();

            var results = await Task.WhenAll(
                endpoints.Select(endpoint => endpointProbeService.ProbeAsync(endpoint, cancellationToken)));

            var unhealthy = results
                .Where(result => !result.Reachable)
                .ToList();

            var incidents = new List<Incident>();

            if (unhealthy.Count > 0)
            {
                using var scope = scopeFactory.CreateScope();

                var incidentService =
                    scope.ServiceProvider
                        .GetRequiredService<IncidentService>();

                foreach (var result in unhealthy)
                {
                    var failureType =
                        DetermineFailureType(result);

                    var severity =
                        DetermineSeverity(result);

                    var incident =
                        await incidentService.CreateOrUpdateAsync(
                            new CreateIncidentRequest(
                                Title:
                                    $"{failureType} detected for {result.Url}",

                                Description:
                                    $"Monitoring Agent detected {result.Status} " +
                                    $"while probing {result.Url}.",

                                Source:
                                    "MonitoringAgent",

                                SourceType:
                                    "Agent",

                                SourceId:
                                    result.EndpointId,

                                Target:
                                    result.Url,

                                TargetType:
                                    "Endpoint",

                                Severity:
                                    severity,

                                FailureType:
                                    failureType,

                                HttpStatusCode:
                                    result.StatusCode,

                                LatencyMs:
                                    result.LatencyMs,

                                ErrorMessage:
                                    result.ErrorMessage,

                                DetectedAt:
                                    result.Timestamp),

                            cancellationToken);

                    incidents.Add(incident);
                }
            }

        var data = new Dictionary<string, object>
{
    ["totalEndpoints"] = results.Length,

    ["healthyEndpoints"] =
        results.Count(x => x.Reachable),

    ["unhealthyEndpoints"] =
        unhealthy.Count,

    ["incidentsCreatedOrUpdated"] =
        incidents.Count,

    ["results"] = results,

    ["incidents"] = incidents
};

            var message = unhealthy.Count == 0
                ? $"All {results.Length} monitored endpoints are healthy."
                : $"{unhealthy.Count} of {results.Length} monitored endpoints are unhealthy.";

            return AgentResult.Successful(
                Name,
                message,
                startedAt,
                data);
        }
        catch (Exception ex)
        {
            Status = AgentStatus.Failed;

            _logger.LogError( ex, "Monitoring Agent failed.");

            return AgentResult.Failed(
                Name,
                ex.Message,
                startedAt);
        }
        finally
        {
            if (Status != AgentStatus.Failed)
            {
                Status = AgentStatus.Stopped;
            }
        }
    }

    private static FailureType DetermineFailureType(
    EndpointProbeResult result)
{
    if (result.Status.Equals(
        "Timeout",
        StringComparison.OrdinalIgnoreCase))
    {
        return FailureType.Timeout;
    }

    if (result.Status.Equals(
        "Unavailable",
        StringComparison.OrdinalIgnoreCase))
    {
        return FailureType.Unavailable;
    }

    if (result.StatusCode.HasValue &&
        result.StatusCode.Value >= 400)
    {
        return FailureType.HttpError;
    }

    return FailureType.Unknown;
}

private static IncidentSeverity DetermineSeverity(
    EndpointProbeResult result)
{
    if (result.StatusCode is >= 500)
    {
        return IncidentSeverity.P2;
    }

    if (result.Status.Equals(
        "Timeout",
        StringComparison.OrdinalIgnoreCase))
    {
        return IncidentSeverity.P2;
    }

    if (result.Status.Equals(
        "Unavailable",
        StringComparison.OrdinalIgnoreCase))
    {
        return IncidentSeverity.P2;
    }

    if (result.StatusCode is >= 400)
    {
        return IncidentSeverity.P3;
    }

    return IncidentSeverity.P4;
}
}