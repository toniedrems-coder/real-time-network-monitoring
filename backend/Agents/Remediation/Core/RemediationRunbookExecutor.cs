using backend.Agents.Remediation.Models;
using backend.Models;
using backend.Services;

using backend.Agents.Remediation.Policies;

namespace backend.Agents.Remediation.Core;

public sealed class RemediationRunbookExecutor
{
    private readonly RemediationRunbookRegistry _registry;
    private readonly RemediationService _remediationService;
    private readonly ILogger<RemediationRunbookExecutor> _logger;
    private readonly IRemediationPolicyEngine _policyEngine;

    public RemediationRunbookExecutor(
        RemediationRunbookRegistry registry,
        RemediationService remediationService,
        ILogger<RemediationRunbookExecutor> logger,
        IRemediationPolicyEngine policyEngine)
    {
        _registry = registry;
        _remediationService = remediationService;
        _logger = logger;
        _policyEngine = policyEngine;
    }

    public async Task<RemediationExecutionResult> ExecuteAsync(
        Guid remediationId,
        CancellationToken cancellationToken = default)
    {
        var remediation =
            await _remediationService.GetByIdAsync(
                remediationId,
                cancellationToken);

        if (remediation is null)
        {
            return RemediationExecutionResult.Failed(
                "Remediation action was not found.",
                $"Remediation {remediationId} does not exist.");
        }

        var policyDecision =
    _policyEngine.Evaluate(remediation);

        if (!policyDecision.ExecutionAllowed)
        {
            _logger.LogWarning(
                "Remediation {RemediationNumber} blocked by policy {PolicyName}. Reason: {Reason}",
                remediation.RemediationNumber,
                policyDecision.PolicyName,
                policyDecision.Reason);

            return RemediationExecutionResult.Failed(
                "Remediation execution blocked by policy.",
                policyDecision.Reason,
                new Dictionary<string, object>
                {
                    ["policyName"] =
                        policyDecision.PolicyName ?? "Unknown",

                    ["riskLevel"] =
                        policyDecision.RiskLevel.ToString(),

                    ["approvalRequired"] =
                        policyDecision.ApprovalRequired,

                    ["escalationRequired"] =
                        policyDecision.EscalationRequired
                });
        }

        var runbook =
            !string.IsNullOrWhiteSpace(
                remediation.RunbookName)
                ? _registry.GetById(
                    remediation.RunbookName)
                : _registry.GetByActionType(
                    remediation.ActionType);

        if (runbook is null)
        {
            return RemediationExecutionResult.Failed(
                "No registered runbook supports this remediation.",
                $"ActionType={remediation.ActionType}");
        }

        if (runbook.ActionType !=
            remediation.ActionType)
        {
            return RemediationExecutionResult.Failed(
                "Runbook action type does not match the remediation action.",
                $"Expected={remediation.ActionType}; Actual={runbook.ActionType}");
        }

        var context =
            new RemediationContext
            {
                IncidentId =
                    remediation.IncidentId,

                RemediationActionId =
                    remediation.Id,

                Target =
                    remediation.Target,

                TargetType =
                    remediation.TargetType,

                ActionType =
                    remediation.ActionType,

                RunbookName =
                    remediation.RunbookName
            };

        _logger.LogInformation(
            "Executing remediation {RemediationNumber} using runbook {RunbookId}.",
            remediation.RemediationNumber,
            runbook.Id);

        return await runbook.ExecuteAsync(
            context,
            cancellationToken);
    }
}