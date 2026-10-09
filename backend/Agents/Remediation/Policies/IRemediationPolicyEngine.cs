using backend.Agents.Remediation.Models;
using backend.Models;

namespace backend.Agents.Remediation.Policies;

public interface IRemediationPolicyEngine
{
    RemediationPolicyDecision Evaluate(
        RemediationAction remediation);

    RemediationPolicyDefinition? GetPolicy(
        RemediationActionType actionType);

    IReadOnlyCollection<RemediationPolicyDefinition>
        GetPolicies();
}
