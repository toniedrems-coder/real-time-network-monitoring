namespace backend.Models;

public sealed class KnowledgeArticle
{
    public Guid Id { get; set; }

    public string ArticleNumber { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public KnowledgeArticleType Type { get; set; }

    public string? Target { get; set; }

    public string? TargetType { get; set; }

    public string? FailureType { get; set; }

    public string? RootCause { get; set; }

    public string? Resolution { get; set; }

    public string? RecommendedActions { get; set; }

    public string? RunbookReference { get; set; }

    public string? Tags { get; set; }

    public Guid? SourceIncidentId { get; set; }

    public double Confidence { get; set; }

    public int TimesMatched { get; set; }

    public int TimesSuccessful { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public DateTimeOffset? LastUsedAt { get; set; }
}