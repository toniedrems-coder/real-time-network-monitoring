namespace backend.Agents.Knowledge.Models;

public sealed record KnowledgeSearchResult
{
    public required Guid IncidentId { get; init; }

    public required string IncidentNumber { get; init; }

    public required string Target { get; init; }

    public required string FailureType { get; init; }

    public bool MatchFound { get; init; }

    public int CandidateCount { get; init; }

    public int MatchCount { get; init; }

    public KnowledgeMatch? BestMatch { get; init; }

    public IReadOnlyCollection<KnowledgeMatch> Matches { get; init; }
        = Array.Empty<KnowledgeMatch>();

    public DateTimeOffset SearchedAt { get; init; }
        = DateTimeOffset.UtcNow;
}