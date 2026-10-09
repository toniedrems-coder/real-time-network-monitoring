using backend.Agents.Knowledge.Models;

namespace backend.Models;

public sealed record IncidentResolutionResult
{
    public required Incident Incident { get; init; }

    public IncidentLearningResult? LearningResult { get; init; }

    public bool LearningAttempted { get; init; }

    public string? LearningMessage { get; init; }
}