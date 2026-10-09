namespace backend.Agents.Remediation.Models;

public sealed record RemediationExecutionResult
{
    public bool Success { get; init; }

    public required string Message { get; init; }

    public string? Output { get; init; }

    public string? FailureReason { get; init; }

    public DateTimeOffset StartedAt { get; init; }

    public DateTimeOffset CompletedAt { get; init; }

    public Dictionary<string, object> Data { get; init; }
        = new();

    public static RemediationExecutionResult Successful(
        string message,
        string? output = null,
        Dictionary<string, object>? data = null)
    {
        var now = DateTimeOffset.UtcNow;

        return new RemediationExecutionResult
        {
            Success = true,
            Message = message,
            Output = output,
            StartedAt = now,
            CompletedAt = now,
            Data = data ?? new()
        };
    }

    public static RemediationExecutionResult Failed(
        string message,
        string? failureReason = null,
        Dictionary<string, object>? data = null)
    {
        var now = DateTimeOffset.UtcNow;

        return new RemediationExecutionResult
        {
            Success = false,
            Message = message,
            FailureReason = failureReason,
            StartedAt = now,
            CompletedAt = now,
            Data = data ?? new()
        };
    }
}