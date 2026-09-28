using backend.Models;

namespace backend.Agents.Knowledge.Models;

public sealed record IncidentLearningResult
{
    public required Guid IncidentId { get; init; }

    public required string IncidentNumber { get; init; }

    public bool Learned { get; init; }

    public bool ExistingKnowledgeUpdated { get; init; }

    public bool NewKnowledgeCreated { get; init; }

    public Guid? KnowledgeArticleId { get; init; }

    public string? ArticleNumber { get; init; }

    public KnowledgeArticleType? ArticleType { get; init; }

    public string? Message { get; init; }

    public DateTimeOffset LearnedAt { get; init; }
        = DateTimeOffset.UtcNow;
}