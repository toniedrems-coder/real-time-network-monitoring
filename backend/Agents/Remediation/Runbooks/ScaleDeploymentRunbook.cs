using backend.Agents.Remediation.Abstractions;
using backend.Agents.Remediation.Models;
using backend.Models;

namespace backend.Agents.Remediation.Runbooks;

public sealed class ScaleDeploymentRunbook : IRemediationRunbook
{
    private readonly IKubernetesOperations _kubernetes;

    public ScaleDeploymentRunbook(IKubernetesOperations kubernetes) => _kubernetes = kubernetes;

    public string Id => "scale-deployment";
    public string Name => "Scale Kubernetes Deployment";
    public RemediationActionType ActionType => RemediationActionType.ScaleDeployment;
    public RemediationRiskLevel RiskLevel => RemediationRiskLevel.High;

    public async Task<RemediationExecutionResult> ExecuteAsync(
        RemediationContext context,
        CancellationToken cancellationToken = default)
    {
        await _kubernetes.ScaleDeploymentAsync(context.Target, cancellationToken);
        return RemediationExecutionResult.Successful(
            $"Deployment {context.Target} was scaled up by one replica, subject to the configured maximum.");
    }
}
