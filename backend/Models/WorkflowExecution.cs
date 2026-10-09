namespace backend.Models;

public sealed class WorkflowExecution
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid AnomalyId { get; set; }

    public Guid? IncidentId { get; set; }

    public Guid? RemediationId { get; set; }

    public string Status { get; set; } = "Pending";

    public string? CurrentStage { get; set; }

    public string? Error { get; set; }

    public int AttemptCount { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? StartedAt { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }

    public string EmailNotificationStatus { get; set; } = "NotConfigured";

    public string SmsNotificationStatus { get; set; } = "NotConfigured";

    public string WebhookNotificationStatus { get; set; } = "NotConfigured";

    public string? NotificationError { get; set; }

    public List<WorkflowStageEvent> StageHistory { get; set; } = [];
}
