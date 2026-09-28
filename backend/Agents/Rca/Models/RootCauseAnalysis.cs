namespace backend.Agents.Rca.Models;

public sealed class RootCauseAnalysis
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public Guid IncidentId { get; init; }

    public string IncidentNumber { get; init; }
        = string.Empty;

    public string Target { get; init; }
        = string.Empty;

    public string ProbableCause { get; init; }
        = string.Empty;

    public double Confidence { get; init; }

    public string ConfidenceLevel { get; init; }
        = string.Empty;

    public List<RcaEvidence> Evidence { get; init; }
        = [];

    public List<string> RecommendedActions { get; init; }
        = [];

    public bool RequiresEscalation { get; init; }

    public DateTimeOffset AnalyzedAt { get; init; }
        = DateTimeOffset.UtcNow;
}