using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace backend.Observability;

/// <summary>
/// Central place for the application's custom OpenTelemetry instrumentation:
/// an <see cref="ActivitySource"/> for custom traces/spans and a <see cref="Meter"/>
/// with instruments for probe latency, Kafka throughput, and anomaly counts.
/// Registered once and injected wherever a probe, Kafka message, or anomaly is produced.
/// </summary>
public sealed class Instrumentation : IDisposable
{
    public const string ServiceName = "network-monitor-backend";

    public Instrumentation()
    {
        ActivitySource = new ActivitySource(ServiceName);
        Meter = new Meter(ServiceName);

        ProbeLatencyHistogram = Meter.CreateHistogram<double>(
            "network_monitor.probe.duration",
            unit: "ms",
            description: "Duration of an endpoint HTTP probe.");

        ProbeResultCounter = Meter.CreateCounter<long>(
            "network_monitor.probe.result",
            description: "Count of endpoint probe results, tagged by outcome.");

        AnomalyCounter = Meter.CreateCounter<long>(
            "network_monitor.anomaly.detected",
            description: "Count of detected anomalies, tagged by type and detection method.");

        KafkaProducedCounter = Meter.CreateCounter<long>(
            "network_monitor.kafka.produced",
            description: "Count of messages produced to Kafka, tagged by topic.");

        KafkaConsumedCounter = Meter.CreateCounter<long>(
            "network_monitor.kafka.consumed",
            description: "Count of messages consumed from Kafka, tagged by topic.");
    }

    public ActivitySource ActivitySource { get; }

    public Meter Meter { get; }

    public Histogram<double> ProbeLatencyHistogram { get; }

    public Counter<long> ProbeResultCounter { get; }

    public Counter<long> AnomalyCounter { get; }

    public Counter<long> KafkaProducedCounter { get; }

    public Counter<long> KafkaConsumedCounter { get; }

    public void Dispose()
    {
        ActivitySource.Dispose();
        Meter.Dispose();
    }
}
