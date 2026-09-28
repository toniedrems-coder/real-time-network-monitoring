namespace backend.Agents.Rca.Models;

public sealed class RcaEvidence
{
    public string Source { get; init; } = string.Empty;

    public string Type { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public string? Value { get; init; }

    public DateTimeOffset CollectedAt { get; init; }
        = DateTimeOffset.UtcNow;
}