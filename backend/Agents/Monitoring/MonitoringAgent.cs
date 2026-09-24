using backend.Agents.Abstractions;
using backend.Agents.Models;

namespace backend.Agents.Monitoring;

public class MonitoringAgent : IAiOpsAgent
{
    private readonly ILogger<MonitoringAgent> _logger;

    public MonitoringAgent(
        ILogger<MonitoringAgent> logger)
    {
        _logger = logger;
    }

    public string Id => "monitoring-agent";

    public string Name => "Monitoring Agent";

    public string Description =>
        "Monitors endpoints, applications and infrastructure health.";

    public AgentStatus Status { get; private set; }
        = AgentStatus.Stopped;

    public bool Enabled => true;

    public async Task<AgentResult> ExecuteAsync(
        AgentContext context,
        CancellationToken cancellationToken = default)
    {
        var startedAt = DateTimeOffset.UtcNow;

        try
        {
            Status = AgentStatus.Running;

            _logger.LogInformation(
                "Monitoring Agent started. ExecutionId: {ExecutionId}",
                context.ExecutionId);

            await Task.Delay(500, cancellationToken);

            var data = new Dictionary<string, object>
            {
                ["target"] = context.Target ?? "all",
                ["targetType"] = context.TargetType ?? "environment",
                ["health"] = "Healthy"
            };

            return AgentResult.Successful(
                Name,
                "Monitoring cycle completed successfully.",
                startedAt,
                data);
        }
        catch (Exception ex)
        {
            Status = AgentStatus.Failed;

            _logger.LogError(
                ex,
                "Monitoring Agent failed.");

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