namespace backend.Models;

public sealed record ApproveRemediationRequest
{
    public string? ApprovedBy { get; init; }
}