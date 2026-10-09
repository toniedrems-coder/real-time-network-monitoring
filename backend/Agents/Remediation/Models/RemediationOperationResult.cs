using backend.Models;

namespace backend.Agents.Remediation.Models;

public sealed record RemediationOperationResult
{
    public required bool Success { get; init; }

    public required string Message { get; init; }

    public RemediationAction? Remediation { get; init; }

    public RemediationExecutionResult? Execution { get; init; }

    public RemediationPolicyDecision? PolicyDecision { get; init; }
}