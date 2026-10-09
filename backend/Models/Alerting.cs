namespace backend.Models;

public sealed class SupportContact
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public bool Active { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public static class AlertTargetTypes
{
    public const string Endpoint = "Endpoint";
    public const string Container = "Container";
}

public static class AlertMetrics
{
    public const string Latency = "Latency";
    public const string ErrorRate = "ErrorRate";
    public const string Availability = "Availability";
    public const string Unreachable = "Unreachable";
    public const string ContainerNotRunning = "ContainerNotRunning";

    public static readonly string[] EndpointMetrics = [Latency, ErrorRate, Availability, Unreachable];
}

public sealed class AlertRule
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string TargetType { get; set; } = AlertTargetTypes.Endpoint;
    // Endpoint id (GUID) for endpoints, container name for containers.
    public string TargetId { get; set; } = string.Empty;
    public string Metric { get; set; } = AlertMetrics.Latency;
    public double Threshold { get; set; }
    public int DurationMinutes { get; set; }
    public string Severity { get; set; } = "High";
    public bool NotifyEmail { get; set; } = true;
    public bool NotifySms { get; set; }
    public List<Guid> ContactIds { get; set; } = new();
    // Minutes between reminders while the alert stays active; 0 sends a single notification.
    public int CooldownMinutes { get; set; } = 60;
    public bool Enabled { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? BreachStartedAt { get; set; }
    public bool Firing { get; set; }
    public DateTimeOffset? LastNotifiedAt { get; set; }
}

public sealed record AlertDelivery(string Contact, string Channel, string Status, string? Error);

public sealed class AlertEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RuleId { get; set; }
    public string RuleName { get; set; } = string.Empty;
    public string Target { get; set; } = string.Empty;
    public string Kind { get; set; } = "Triggered";
    public string Severity { get; set; } = "High";
    public string Message { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public List<AlertDelivery> Deliveries { get; set; } = new();
}

public sealed class SupportContactRequest
{
    public string? Name { get; init; }
    public string? Email { get; init; }
    public string? Phone { get; init; }
    public bool Active { get; init; } = true;
}

public sealed class AlertRuleRequest
{
    public string? Name { get; init; }
    public string? TargetType { get; init; }
    public string? TargetId { get; init; }
    public string? Metric { get; init; }
    public double Threshold { get; init; }
    public int DurationMinutes { get; init; }
    public string? Severity { get; init; }
    public bool NotifyEmail { get; init; }
    public bool NotifySms { get; init; }
    public List<Guid>? ContactIds { get; init; }
    public int CooldownMinutes { get; init; }
    public bool Enabled { get; init; } = true;
}
