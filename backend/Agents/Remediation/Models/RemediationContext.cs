using backend.Models;

namespace backend.Agents.Remediation.Models;

public sealed record RemediationContext
{
    public required Guid IncidentId { get; init; }

    public required Guid RemediationActionId { get; init; }

    public required string Target { get; init; }

    public required string TargetType { get; init; }

    public required RemediationActionType ActionType { get; init; }

    public string? RunbookName { get; init; }

    public Dictionary<string, object> Data { get; init; }
        = new();
}