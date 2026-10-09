namespace backend.Models;

public sealed class IncidentKnowledgeMatch
{
    public Guid Id { get; set; }

    public Guid IncidentId { get; set; }

    public Guid KnowledgeArticleId { get; set; }

    public double MatchScore { get; set; }

    public string MatchLevel { get; set; } = "Low";

    public string? MatchReasons { get; set; }

    public bool WasSelected { get; set; }

    public bool? WasSuccessful { get; set; }

    public DateTimeOffset MatchedAt { get; set; }

    public DateTimeOffset? VerifiedAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public Incident? Incident { get; set; }

    public KnowledgeArticle? KnowledgeArticle { get; set; }

    public DateTimeOffset? LearningAppliedAt
    {
        get;
        set;
    }

}