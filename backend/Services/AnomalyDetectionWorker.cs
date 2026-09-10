using backend.Messaging;
using backend.Models;
using backend.Observability;
using Microsoft.Extensions.Options;

namespace backend.Services;

/// <summary>
/// Consumer-side worker: consumes endpoint KPI messages from Kafka
/// (<see cref="KafkaOptions.MetricsTopic"/>), runs both rule-based/statistical anomaly
/// detection and an ML.NET model against the endpoint's rolling history, and publishes
/// any detected anomalies to Kafka (<see cref="KafkaOptions.AnomaliesTopic"/>) for
/// downstream consumers (dashboard, alerting, future AIOps automation).
/// </summary>
public sealed class AnomalyDetectionWorker : KafkaConsumerWorker<EndpointMetrics>
{
    private readonly MetricsStore metricsStore;
    private readonly AnomalyDetectionService anomalyDetectionService;
    private readonly MlAnomalyDetectionService mlAnomalyDetectionService;
    private readonly KafkaProducerService kafkaProducer;
    private readonly KafkaOptions kafkaOptions;
    private readonly Instrumentation instrumentation;

    public AnomalyDetectionWorker(
        MetricsStore metricsStore,
        AnomalyDetectionService anomalyDetectionService,
        MlAnomalyDetectionService mlAnomalyDetectionService,
        KafkaProducerService kafkaProducer,
        IOptions<KafkaOptions> kafkaOptions,
        ILogger<AnomalyDetectionWorker> logger,
        Instrumentation instrumentation)
        : base(kafkaOptions, logger, instrumentation)
    {
        this.metricsStore = metricsStore;
        this.anomalyDetectionService = anomalyDetectionService;
        this.mlAnomalyDetectionService = mlAnomalyDetectionService;
        this.kafkaProducer = kafkaProducer;
        this.kafkaOptions = kafkaOptions.Value;
        this.instrumentation = instrumentation;
    }

    protected override string Topic => kafkaOptions.MetricsTopic;

    protected override string ConsumerGroupId => kafkaOptions.AnomalyConsumerGroup;

    protected override async Task HandleAsync(EndpointMetrics message, CancellationToken cancellationToken)
    {
        // MetricsIngestionWorker (separate consumer group on the same topic) is
        // responsible for recording history into MetricsStore; by the time this
        // handler runs the two workers race independently, so fold the incoming
        // message into the read used for detection to avoid depending on ordering.
        var history = metricsStore.GetHistory(message.EndpointId);
        if (!history.Contains(message))
        {
            history = [.. history, message];
        }

        var ruleAnomalies = anomalyDetectionService.Detect(message.EndpointId, history);
        var mlAnomalies = mlAnomalyDetectionService.Detect(message.EndpointId, history);

        foreach (var anomaly in ruleAnomalies.Concat(mlAnomalies))
        {
            instrumentation.AnomalyCounter.Add(
                1,
                new KeyValuePair<string, object?>("type", anomaly.Type),
                new KeyValuePair<string, object?>("detectionMethod", anomaly.DetectionMethod));

            await kafkaProducer.PublishAsync(
                kafkaOptions.AnomaliesTopic,
                anomaly.EndpointId.ToString(),
                anomaly,
                cancellationToken);
        }
    }
}
