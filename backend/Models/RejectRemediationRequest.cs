namespace backend.Models;

public sealed record RejectRemediationRequest
{
    public string? RejectedBy { get; init; }

    public string? Reason { get; init; }
}