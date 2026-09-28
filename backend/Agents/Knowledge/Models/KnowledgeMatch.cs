using backend.Models;

namespace backend.Agents.Knowledge.Models;

public sealed record KnowledgeMatch
{
    public required Guid ArticleId { get; init; }

    public required string ArticleNumber { get; init; }

    public required string Title { get; init; }

    public required KnowledgeArticleType Type { get; init; }

    public string? Target { get; init; }

    public string? FailureType { get; init; }

    public string? RootCause { get; init; }

    public string? Resolution { get; init; }

    public string? RecommendedActions { get; init; }

    public string? RunbookReference { get; init; }

    public string? Tags { get; init; }

    public double ArticleConfidence { get; init; }

    public double MatchScore { get; init; }

    public string MatchLevel { get; init; } = "Low";

    public IReadOnlyCollection<string> MatchReasons { get; init; }
        = Array.Empty<string>();
}