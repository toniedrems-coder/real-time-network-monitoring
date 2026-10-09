using backend.Agents.Remediation.Abstractions;
using backend.Agents.Remediation.Models;
using backend.Models;

namespace backend.Agents.Remediation.Runbooks;

public sealed class RestartDeploymentRunbook : IRemediationRunbook
{
    private readonly IKubernetesOperations _kubernetes;

    public RestartDeploymentRunbook(IKubernetesOperations kubernetes) => _kubernetes = kubernetes;

    public string Id => "restart-deployment";
    public string Name => "Restart Kubernetes Deployment";
    public RemediationActionType ActionType => RemediationActionType.RestartDeployment;
    public RemediationRiskLevel RiskLevel => RemediationRiskLevel.High;

    public async Task<RemediationExecutionResult> ExecuteAsync(
        RemediationContext context,
        CancellationToken cancellationToken = default)
    {
        await _kubernetes.RestartDeploymentAsync(context.Target, cancellationToken);
        return RemediationExecutionResult.Successful(
            $"Restart requested for deployment {context.Target}.");
    }
}
