namespace backend.Models;

public sealed partial record DetectedAnomaly(
    Guid Id,
    Guid EndpointId,
    string Type,
    string Severity,
    string Description,
    DateTimeOffset DetectedAt,
    double ObservedValue,
    double ExpectedValue);

public partial record DetectedAnomaly
{
    public Guid MetricId { get; init; }
}
