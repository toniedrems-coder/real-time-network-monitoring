namespace backend.Models;

public enum RemediationStatus
{
    Proposed = 0,
    AwaitingApproval = 1,
    Approved = 2,
    Executing = 3,
    Verifying = 4,
    Succeeded = 5,
    Failed = 6,
    Rejected = 7,
    Escalated = 8
}