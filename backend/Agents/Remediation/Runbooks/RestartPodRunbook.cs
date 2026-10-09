using backend.Agents.Remediation.Abstractions;
using backend.Agents.Remediation.Models;
using backend.Models;

namespace backend.Agents.Remediation.Runbooks;

public sealed class RestartPodRunbook : IRemediationRunbook
{
    private readonly IKubernetesOperations _kubernetes;

    public RestartPodRunbook(IKubernetesOperations kubernetes) => _kubernetes = kubernetes;

    public string Id => "restart-pod";
    public string Name => "Restart Kubernetes Pod";
    public RemediationActionType ActionType => RemediationActionType.RestartPod;
    public RemediationRiskLevel RiskLevel => RemediationRiskLevel.Medium;

    public async Task<RemediationExecutionResult> ExecuteAsync(
        RemediationContext context,
        CancellationToken cancellationToken = default)
    {
        await _kubernetes.RestartPodAsync(context.Target, cancellationToken);
        return RemediationExecutionResult.Successful(
            $"Pod {context.Target} was deleted; its controller will recreate it.");
    }
}
