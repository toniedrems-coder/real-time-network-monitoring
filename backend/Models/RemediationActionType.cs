namespace backend.Models;

public enum RemediationActionType
{
    RecheckEndpoint = 0,
    RestartService = 1,
    RestartPod = 2,
    RestartDeployment = 3,
    ScaleDeployment = 4,
    ClearCache = 5,
    ExecuteRunbook = 6
}