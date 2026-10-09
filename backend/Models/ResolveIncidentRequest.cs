namespace backend.Models;

public sealed record ResolveIncidentRequest
{
    public required string Resolution { get; init; }

    /// <summary>
    /// true  = selected knowledge helped resolve the incident
    /// false = selected knowledge did not resolve the incident
    /// null  = outcome cannot be attributed to selected knowledge
    /// </summary>
    public bool? KnowledgeSuccessful { get; init; }
}