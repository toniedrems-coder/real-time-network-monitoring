namespace backend.Models;

public sealed class WorkflowStageEvent
{
    public string Name { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public DateTimeOffset StartedAt { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }

    public string? Details { get; set; }
}
