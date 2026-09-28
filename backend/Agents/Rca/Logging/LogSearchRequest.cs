namespace backend.Agents.Rca.Logging;

public sealed record LogSearchRequest
{
    public string? Target { get; init; }

    public DateTimeOffset From { get; init; }

    public DateTimeOffset To { get; init; }

    public IReadOnlyCollection<string> Levels { get; init; } =
        Array.Empty<string>();

    public int MaximumResults { get; init; } = 100;
}