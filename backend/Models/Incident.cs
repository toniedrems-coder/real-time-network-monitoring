namespace backend.Models;

public sealed class Incident
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string IncidentNumber { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string Source { get; set; } = string.Empty;

    public string SourceType { get; set; } = string.Empty;

    public Guid? SourceId { get; set; }

    public string Target { get; set; } = string.Empty;

    public string TargetType { get; set; } = string.Empty;

    public IncidentSeverity Severity { get; set; }

    public IncidentStatus Status { get; set; }

    public FailureType FailureType { get; set; }

    public int? HttpStatusCode { get; set; }

    public double? LatencyMs { get; set; }

    public string? ErrorMessage { get; set; }

    public DateTimeOffset DetectedAt { get; set; }

    public DateTimeOffset? AcknowledgedAt { get; set; }

    public DateTimeOffset? ResolvedAt { get; set; }

    public string? AssignedAgent { get; set; }

    public string? RootCause { get; set; }

    public string? RecommendedAction { get; set; }

    public string? Resolution { get; set; }

    public bool RequiresApproval { get; set; }

    public bool RemediationAttempted { get; set; }

    public bool RemediationSuccessful { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}