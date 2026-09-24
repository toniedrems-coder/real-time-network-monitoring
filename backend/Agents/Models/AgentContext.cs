namespace backend.Agents.Models;

public class AgentContext
{
    public Guid ExecutionId { get; init; } = Guid.NewGuid();

    public string? IncidentId { get; init; }

    public string? Target { get; init; }

/// <summary>
/// Endpoint 
/// Application
/// Pod
/// Deployment
/// Container
/// Database
/// IP
/// Domain
/// Service
/// </summary>
/// <value></value>
    public string? TargetType { get; init; }  

    public Dictionary<string, object> Data { get; init; } = new();
}
