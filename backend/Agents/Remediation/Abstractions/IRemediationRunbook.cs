using backend.Models;

using backend.Agents.Remediation.Models;

namespace backend.Agents.Remediation.Abstractions;

public interface IRemediationRunbook
{
    string Id { get; }

    string Name { get; }

    RemediationActionType ActionType { get; }

    RemediationRiskLevel RiskLevel { get; }

    Task<RemediationExecutionResult> ExecuteAsync(
        RemediationContext context,
        CancellationToken cancellationToken = default);
}