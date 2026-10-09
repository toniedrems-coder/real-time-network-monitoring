using backend.Models;

namespace backend.Agents.Remediation.Models;

public sealed record RemediationPolicyDecision
{
    public required RemediationActionType ActionType { get; init; }

    public required RemediationRiskLevel RiskLevel { get; init; }

    public required bool ExecutionAllowed { get; init; }

    public required bool ApprovalRequired { get; init; }

    public required bool EscalationRequired { get; init; }

    public required string Reason { get; init; }

    public string? PolicyName { get; init; }

    public DateTimeOffset EvaluatedAt { get; init; }
        = DateTimeOffset.UtcNow;
}