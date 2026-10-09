namespace backend.Agents.Remediation.Models;

public sealed record RemediationVerificationResult
{
    public required Guid RemediationId { get; init; }

    public required Guid IncidentId { get; init; }

    public required bool Successful { get; init; }

    public required string Message { get; init; }

    public string? Evidence { get; init; }

    public double? LatencyMs { get; init; }

    public int? StatusCode { get; init; }

    public DateTimeOffset VerifiedAt { get; init; } =
        DateTimeOffset.UtcNow;
}