using backend.Models;

namespace backend.Agents.Remediation.Models;

public sealed record RemediationRecommendation
{
    public required RemediationActionType ActionType { get; init; }

    public required string RunbookName { get; init; }

    public required string Reason { get; init; }

    public double Confidence { get; init; }

    public string? Source { get; init; }
}