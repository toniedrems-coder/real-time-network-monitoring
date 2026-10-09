namespace backend.Models;

public sealed class RemediationAction
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string RemediationNumber { get; set; } = string.Empty;

    public Guid IncidentId { get; set; }

    public string Target { get; set; } = string.Empty;

    public string TargetType { get; set; } = string.Empty;

    public RemediationActionType ActionType { get; set; }

    public RemediationRiskLevel RiskLevel { get; set; }

    public RemediationStatus Status { get; set; }
        = RemediationStatus.Proposed;

    public RemediationApprovalStatus ApprovalStatus { get; set; }
        = RemediationApprovalStatus.NotRequired;

    public string? RunbookName { get; set; }

    public string? Reason { get; set; }

    public string? RequestedBy { get; set; }

    public string? ApprovedBy { get; set; }

    public string? ExecutionOutput { get; set; }

    public string? FailureReason { get; set; }

    public bool? VerificationSuccessful { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
        = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; set; }
        = DateTimeOffset.UtcNow;

    public DateTimeOffset? ApprovedAt { get; set; }

    public DateTimeOffset? StartedAt { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }

    public DateTimeOffset? VerifiedAt { get; set; }

    public Incident? Incident { get; set; }

    public string? RejectedBy { get; set; }

    public string? RejectionReason { get; set; }

}