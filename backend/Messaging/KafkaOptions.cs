namespace backend.Messaging;

/// <summary>
/// Kafka connection and topic configuration bound from the "Kafka" configuration section.
/// </summary>
public sealed class KafkaOptions
{
    public const string SectionName = "Kafka";

    /// <summary>Comma-separated list of Kafka bootstrap brokers, e.g. "localhost:9092".</summary>
    public string BootstrapServers { get; set; } = "localhost:9092";

    /// <summary>Topic that endpoint probe results (KPIs) are published to.</summary>
    public string MetricsTopic { get; set; } = "endpoint.metrics";

    /// <summary>Topic that detected anomalies are published to.</summary>
    public string AnomaliesTopic { get; set; } = "endpoint.anomalies";

    /// <summary>Consumer group id used by the metrics ingestion worker.</summary>
    public string MetricsConsumerGroup { get; set; } = "network-monitor-metrics-ingestion";

    /// <summary>Consumer group id used by the anomaly detection worker.</summary>
    public string AnomalyConsumerGroup { get; set; } = "network-monitor-anomaly-detection";
}
