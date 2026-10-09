namespace backend.Models;

public sealed record UpdateRemediationRequest
{
    //   public RemediationStatus? Status { get; init; }

    //  public RemediationApprovalStatus? ApprovalStatus { get; init; }

    //   public string? ApprovedBy { get; init; }

    public string? ExecutionOutput { get; init; }

    public string? FailureReason { get; init; }

    public bool? VerificationSuccessful { get; init; }
}