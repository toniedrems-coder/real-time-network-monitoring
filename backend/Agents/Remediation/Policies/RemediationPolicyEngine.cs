using backend.Agents.Remediation.Models;
using backend.Models;

namespace backend.Agents.Remediation.Policies;

public sealed class RemediationPolicyEngine
    : IRemediationPolicyEngine
{
    private readonly IReadOnlyDictionary<
        RemediationActionType,
        RemediationPolicyDefinition> _policies;

    public RemediationPolicyEngine()
    {
        var policies =
            new List<RemediationPolicyDefinition>
            {
                new()
                {
                    Name = "Endpoint Recheck Policy",

                    ActionType =
                        RemediationActionType.RecheckEndpoint,

                    RiskLevel =
                        RemediationRiskLevel.Low,

                    ApprovalRequired = false,

                    AutoExecutionAllowed = true,

                    EscalationRequired = false,

                    Description =
                        "Read-only endpoint health verification."
                },

                new()
                {
                    Name = "Service Restart Policy",

                    ActionType =
                        RemediationActionType.RestartService,

                    RiskLevel =
                        RemediationRiskLevel.Medium,

                    ApprovalRequired = true,

                    AutoExecutionAllowed = false,

                    EscalationRequired = false,

                    Description =
                        "Restarting a service can temporarily interrupt traffic."
                },

                new()
                {
                    Name = "Pod Restart Policy",

                    ActionType =
                        RemediationActionType.RestartPod,

                    RiskLevel =
                        RemediationRiskLevel.Medium,

                    ApprovalRequired = true,

                    AutoExecutionAllowed = false,

                    EscalationRequired = false,

                    Description =
                        "Restarting a Kubernetes pod requires human approval."
                },

                new()
                {
                    Name = "Deployment Restart Policy",

                    ActionType =
                        RemediationActionType.RestartDeployment,

                    RiskLevel =
                        RemediationRiskLevel.High,

                    ApprovalRequired = true,

                    AutoExecutionAllowed = false,

                    EscalationRequired = true,

                    Description =
                        "Deployment restart can affect multiple workloads."
                },

                new()
                {
                    Name = "Deployment Scaling Policy",

                    ActionType =
                        RemediationActionType.ScaleDeployment,

                    RiskLevel =
                        RemediationRiskLevel.High,

                    ApprovalRequired = true,

                    AutoExecutionAllowed = false,

                    EscalationRequired = true,

                    Description =
                        "Changing deployment capacity is a high-impact infrastructure operation."
                },

                new()
                {
                    Name = "Cache Clear Policy",

                    ActionType =
                        RemediationActionType.ClearCache,

                    RiskLevel =
                        RemediationRiskLevel.Medium,

                    ApprovalRequired = true,

                    AutoExecutionAllowed = false,

                    EscalationRequired = false,

                    Description =
                        "Clearing application cache may temporarily affect performance."
                },

                new()
                {
                    Name = "Generic Runbook Policy",

                    ActionType =
                        RemediationActionType.ExecuteRunbook,

                    RiskLevel =
                        RemediationRiskLevel.High,

                    ApprovalRequired = true,

                    AutoExecutionAllowed = false,

                    EscalationRequired = true,

                    Description =
                        "Generic runbooks require explicit review before execution."
                }
            };

        _policies =
            policies.ToDictionary(
                x => x.ActionType);
    }

    public RemediationPolicyDecision Evaluate(
        RemediationAction remediation)
    {
        if (!_policies.TryGetValue(
                remediation.ActionType,
                out var policy))
        {
            return new RemediationPolicyDecision
            {
                ActionType =
                    remediation.ActionType,

                RiskLevel =
                    RemediationRiskLevel.High,

                ExecutionAllowed = false,

                ApprovalRequired = true,

                EscalationRequired = true,

                PolicyName =
                    "Default Deny Policy",

                Reason =
                    "No remediation policy exists for this action type."
            };
        }

        var approved =
            remediation.ApprovalStatus ==
            RemediationApprovalStatus.Approved;


        var executionAllowed =
            !policy.EscalationRequired &&
            (
                policy.AutoExecutionAllowed ||
                (policy.ApprovalRequired && approved)
            );

        return new RemediationPolicyDecision
        {
            ActionType =
                remediation.ActionType,

            RiskLevel =
                policy.RiskLevel,

            ApprovalRequired =
                policy.ApprovalRequired,

            EscalationRequired =
                policy.EscalationRequired,

            ExecutionAllowed =
                executionAllowed,

            PolicyName =
                policy.Name,

            Reason =
                BuildReason(
                    policy,
                    approved,
                    executionAllowed)
        };
    }

    public RemediationPolicyDefinition? GetPolicy(
        RemediationActionType actionType)
    {
        return _policies.TryGetValue(
            actionType,
            out var policy)
                ? policy
                : null;
    }

    public IReadOnlyCollection<RemediationPolicyDefinition>
        GetPolicies()
    {
        return _policies.Values.ToList();
    }

    private static string BuildReason(
        RemediationPolicyDefinition policy,
        bool approved,
        bool executionAllowed)
    {
        if (policy.AutoExecutionAllowed)
        {
            return
                "Policy allows automatic execution for this low-risk action.";
        }

        if (policy.ApprovalRequired && !approved)
        {
            return
                "Execution is blocked until the required human approval is provided.";
        }

        if (policy.ApprovalRequired &&
            approved &&
            executionAllowed)
        {
            return
                "Required human approval has been provided. Execution is permitted.";
        }

        if (policy.ApprovalRequired && approved && executionAllowed)
        {
            return
                "Required human approval has been provided. Execution is permitted.";
        }

        return
            "Execution is blocked by remediation policy.";
    }
}