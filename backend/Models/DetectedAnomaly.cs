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

    /// <summary>How the anomaly was detected: "Rule" (statistical baseline) or "MachineLearning" (ML.NET SSA model).</summary>
    public string DetectionMethod { get; init; } = "Rule";
}
