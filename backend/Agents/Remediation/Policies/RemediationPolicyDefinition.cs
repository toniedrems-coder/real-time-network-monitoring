using backend.Models;

namespace backend.Agents.Remediation.Policies;

public sealed record RemediationPolicyDefinition
{
    public required string Name { get; init; }

    public required RemediationActionType ActionType { get; init; }

    public required RemediationRiskLevel RiskLevel { get; init; }

    public required bool ApprovalRequired { get; init; }

    public required bool AutoExecutionAllowed { get; init; }

    public required bool EscalationRequired { get; init; }

    public required string Description { get; init; }
}