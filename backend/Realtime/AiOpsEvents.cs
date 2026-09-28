namespace backend.Realtime;

public sealed record AgentEvent
{
    public required string AgentId { get; init; }

    public required string AgentName { get; init; }

    public required string Status { get; init; }

    public string? Message { get; init; }

    public DateTimeOffset Timestamp { get; init; }
        = DateTimeOffset.UtcNow;

    public object? Data { get; init; }
}

public sealed record IncidentEvent
{
    public required Guid IncidentId { get; init; }

    public required string IncidentNumber { get; init; }

    public required string Target { get; init; }

    public required string Status { get; init; }

    public required string Severity { get; init; }

    public required string FailureType { get; init; }

    public string? Message { get; init; }

    public DateTimeOffset Timestamp { get; init; }
        = DateTimeOffset.UtcNow;
}

public sealed record RcaEvent
{
    public required Guid IncidentId { get; init; }

    public required string IncidentNumber { get; init; }

    public required string Status { get; init; }

    public string? Message { get; init; }

    public double? Confidence { get; init; }

    public string? ConfidenceLevel { get; init; }

    public bool? RequiresEscalation { get; init; }

    public DateTimeOffset Timestamp { get; init; }
        = DateTimeOffset.UtcNow;
}