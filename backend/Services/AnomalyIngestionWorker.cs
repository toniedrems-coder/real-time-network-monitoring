using backend.Hubs;
using backend.Messaging;
using backend.Models;
using backend.Observability;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;

namespace backend.Services;

/// <summary>
/// Consumer-side worker: reads detected anomalies from Kafka
/// (<see cref="KafkaOptions.AnomaliesTopic"/>), updates the shared
/// <see cref="AnomalyStore"/> that the REST API reads from, and pushes the
/// anomaly live to subscribed SignalR clients.
/// </summary>
public sealed class AnomalyIngestionWorker : KafkaConsumerWorker<DetectedAnomaly>
{
    private readonly AnomalyStore anomalyStore;
    private readonly IHubContext<MetricsHub> hubContext;
    private readonly KafkaOptions kafkaOptions;

    public AnomalyIngestionWorker(
        AnomalyStore anomalyStore,
        IHubContext<MetricsHub> hubContext,
        IOptions<KafkaOptions> kafkaOptions,
        ILogger<AnomalyIngestionWorker> logger,
        Instrumentation instrumentation)
        : base(kafkaOptions, logger, instrumentation)
    {
        this.anomalyStore = anomalyStore;
        this.hubContext = hubContext;
        this.kafkaOptions = kafkaOptions.Value;
    }

    protected override string Topic => kafkaOptions.AnomaliesTopic;

    protected override string ConsumerGroupId => "network-monitor-anomaly-ingestion";

    protected override async Task HandleAsync(DetectedAnomaly message, CancellationToken cancellationToken)
    {
        anomalyStore.Record(message);

        await hubContext.Clients
            .Group(message.EndpointId.ToString())
            .SendAsync("AnomalyDetected", message, cancellationToken);
    }
}
