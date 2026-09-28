namespace backend.Agents.Models;

public sealed record InvestigationWorkflowResult
{
    public required Guid IncidentId { get; init; }

    public bool Success { get; init; }

    public AgentResult? RcaResult { get; init; }

    public AgentResult? KnowledgeResult { get; init; }

    public DateTimeOffset StartedAt { get; init; }

    public DateTimeOffset CompletedAt { get; init; }
}