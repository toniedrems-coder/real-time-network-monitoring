namespace backend.Agents.Remediation.Abstractions;

public interface IKubernetesOperations
{
    Task RestartPodAsync(string target, CancellationToken cancellationToken);

    Task RestartDeploymentAsync(string target, CancellationToken cancellationToken);

    Task ScaleDeploymentAsync(string target, CancellationToken cancellationToken);
}
