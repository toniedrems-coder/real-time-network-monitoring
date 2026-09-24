namespace backend.Models;

public sealed record CreateIncidentRequest(
    string Title,
    string? Description,
    string Source,
    string SourceType,
    Guid? SourceId,
    string Target,
    string TargetType,
    IncidentSeverity Severity,
    FailureType FailureType,
    int? HttpStatusCode,
    double? LatencyMs,
    string? ErrorMessage,
    DateTimeOffset DetectedAt);
    