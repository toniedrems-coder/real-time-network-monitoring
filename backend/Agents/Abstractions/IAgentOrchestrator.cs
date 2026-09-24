using backend.Agents.Models;

namespace backend.Agents.Abstractions;

public interface IAgentOrchestrator
{
    Task<AgentResult> ExecuteAgentAsync(
        string agentId,
        AgentContext context,
        CancellationToken cancellationToken = default);
}