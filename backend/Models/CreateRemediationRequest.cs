using backend.Models;

public sealed record CreateRemediationRequest
{
    public required Guid IncidentId { get; init; }

    public required RemediationActionType ActionType { get; init; }

    public string? Target { get; init; }

    public string? RunbookName { get; init; }

    public string? Reason { get; init; }

    public string? RequestedBy { get; init; }
}