namespace backend.Agents.Rca.Logging;

public sealed record LogEntry
{
    public DateTimeOffset Timestamp { get; init; }

    public string Level { get; init; } = "Information";

    public string Source { get; init; } = string.Empty;

    public string Message { get; init; } = string.Empty;

    public string? ExceptionType { get; init; }

    public string? TraceId { get; init; }

    public string? CorrelationId { get; init; }

    public string? Target { get; init; }

    public Dictionary<string, string> Properties { get; init; } =
        new();
}