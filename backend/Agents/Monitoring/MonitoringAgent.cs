using backend.Agents.Abstractions;
using backend.Agents.Models;
using backend.Agents.Tools.Monitoring;
using backend.Services;

namespace backend.Agents.Monitoring;

public class MonitoringAgent : IAiOpsAgent
{
    private readonly ILogger<MonitoringAgent> _logger;
    private readonly EndpointService endpointService;
    private readonly IEndpointProbeService endpointProbeService;

    public MonitoringAgent(
        ILogger<MonitoringAgent> logger,
        EndpointService endpointService,
        IEndpointProbeService endpointProbeService)
    {
        _logger = logger;   
        this.endpointService = endpointService;
        this.endpointProbeService = endpointProbeService;   
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

            var data = new Dictionary<string, object>
            {
                ["totalEndpoints"] = results.Length,
                ["healthyEndpoints"] = results.Count(x => x.Reachable),
                ["unhealthyEndpoints"] = unhealthy.Count,
                ["results"] = results
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
}