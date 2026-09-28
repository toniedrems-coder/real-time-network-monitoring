using backend.Agents.Abstractions;
using backend.Agents.Models;
using backend.Realtime;

namespace backend.Agents.Core;

public class AgentOrchestrator : IAgentOrchestrator
{
    private readonly AgentRegistry _registry;
    private readonly ILogger<AgentOrchestrator> _logger;

    private readonly IAiOpsEventPublisher eventPublisher;

    public AgentOrchestrator(
        AgentRegistry registry,
        IAiOpsEventPublisher eventPublisher,
        ILogger<AgentOrchestrator> logger)
    {
        _registry = registry;
        _logger = logger;
        this.eventPublisher = eventPublisher;
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
        await eventPublisher.AgentStartedAsync(
        new AgentEvent
        {
            AgentId = agent.Id,
            AgentName = agent.Name,
            Status = agent.Id == "rca-agent"
                ? "Investigating"
                : "Executing",
            Message =
                $"{agent.Name} execution started."
        },
        cancellationToken);

        try
        {
            var result =
                await agent.ExecuteAsync(
                    context,
                    cancellationToken);

            if (result.Success)
            {
                await eventPublisher.AgentCompletedAsync(
                    new AgentEvent
                    {
                        AgentId = agent.Id,
                        AgentName = agent.Name,
                        Status = "Stopped",
                        Message = result.Message,
                        Data = result.Data
                    },
                    cancellationToken);
            }
            else
            {
                await eventPublisher.AgentFailedAsync(
                    new AgentEvent
                    {
                        AgentId = agent.Id,
                        AgentName = agent.Name,
                        Status = "Failed",
                        Message = result.Message
                    },
                    cancellationToken);
            }

            return result;
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            await eventPublisher.AgentFailedAsync(
                new AgentEvent
                {
                    AgentId = agent.Id,
                    AgentName = agent.Name,
                    Status = "Failed",
                    Message = exception.Message
                },
                CancellationToken.None);

            throw;
        }


        //  return await agent.ExecuteAsync(context, cancellationToken);
    }

    public async Task<AgentResult> ExecuteInvestigationAsync(
    AgentContext context,
    CancellationToken cancellationToken = default)
    {
        var startedAt =
            DateTimeOffset.UtcNow;

        if (string.IsNullOrWhiteSpace(context.IncidentId))
        {
            return AgentResult.Failed(
                "Investigation Workflow",
                "IncidentId is required.",
                startedAt);
        }

        if (!Guid.TryParse(
                context.IncidentId,
                out var incidentId))
        {
            return AgentResult.Failed(
                "Investigation Workflow",
                "IncidentId is not a valid GUID.",
                startedAt);
        }

        // STEP 1 — RCA
        var rcaResult =
            await ExecuteAgentAsync(
                "rca-agent",
                context,
                cancellationToken);

        if (!rcaResult.Success)
        {
            return AgentResult.Failed(
                "Investigation Workflow",
                $"RCA failed for incident {incidentId}. Knowledge search was not executed.",
                startedAt);
        }

        // STEP 2 — Knowledge search
        var knowledgeResult =
            await ExecuteAgentAsync(
                "knowledge-agent",
                context,
                cancellationToken);

        /*
         * KnowledgeResult.Success means the agent itself
         * executed successfully.
         *
         * It does NOT necessarily mean a knowledge article
         * was found.
         */

        if (!knowledgeResult.Success)
        {
            return AgentResult.Failed(
                "Investigation Workflow",
                $"RCA completed, but Knowledge Agent failed for incident {incidentId}.",
                startedAt);
        }

        var data =
            new Dictionary<string, object>
            {
                ["incidentId"] =
                    incidentId,

                ["rcaCompleted"] =
                    true,

                ["knowledgeSearchCompleted"] =
                    true,

                ["rca"] =
                    rcaResult,

                ["knowledge"] =
                    knowledgeResult
            };

        return AgentResult.Successful(
            "Investigation Workflow",
            "RCA and knowledge search completed successfully.",
            startedAt,
            data);
    }
}