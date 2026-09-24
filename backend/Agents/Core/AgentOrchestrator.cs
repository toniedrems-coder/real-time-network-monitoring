using backend.Agents.Abstractions;
using backend.Agents.Models;

namespace backend.Agents.Core;

public class AgentOrchestrator : IAgentOrchestrator
{
    private readonly AgentRegistry _registry;
    private readonly ILogger<AgentOrchestrator> _logger;

    public AgentOrchestrator(
        AgentRegistry registry,
        ILogger<AgentOrchestrator> logger)
    {
        _registry = registry;
        _logger = logger;
    }

    public async Task<AgentResult> ExecuteAgentAsync(
        string agentId,
        AgentContext context,
        CancellationToken cancellationToken = default)
    {
        var agent = _registry.GetById(agentId);

        if (agent is null)
        {
            throw new InvalidOperationException(
                $"Agent '{agentId}' is not registered.");
        }

        if (!agent.Enabled)
        {
            throw new InvalidOperationException(
                $"Agent '{agent.Name}' is disabled.");
        }

        _logger.LogInformation(
            "Executing AIOps agent {AgentName}. ExecutionId: {ExecutionId}",
            agent.Name,
            context.ExecutionId);

        return await agent.ExecuteAsync(context, cancellationToken);
    }
}