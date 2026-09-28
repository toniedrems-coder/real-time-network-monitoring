namespace backend.Models;

public sealed class UpdateKnowledgeArticleRequest
{
    public string? Title { get; set; }

    public string? Description { get; set; }

    public KnowledgeArticleType? Type { get; set; }

    public string? Target { get; set; }

    public string? TargetType { get; set; }

    public string? FailureType { get; set; }

    public string? RootCause { get; set; }

    public string? Resolution { get; set; }

    public string? RecommendedActions { get; set; }

    public string? RunbookReference { get; set; }

    public string? Tags { get; set; }

    public double? Confidence { get; set; }

    public bool? IsActive { get; set; }
}