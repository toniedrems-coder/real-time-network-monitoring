using backend.Hubs;
using backend.Messaging;
using backend.Models;
using backend.Observability;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;

namespace backend.Services;

/// <summary>
/// Consumer-side worker: reads endpoint KPI messages from Kafka
/// (<see cref="KafkaOptions.MetricsTopic"/>), updates the shared <see cref="MetricsStore"/>
/// that the REST API reads from, and pushes the update live to subscribed SignalR clients.
/// </summary>
public sealed class MetricsIngestionWorker : KafkaConsumerWorker<EndpointMetrics>
{
    private readonly MetricsStore metricsStore;
    private readonly IHubContext<MetricsHub> hubContext;
    private readonly KafkaOptions kafkaOptions;

    public MetricsIngestionWorker(
        MetricsStore metricsStore,
        IHubContext<MetricsHub> hubContext,
        IOptions<KafkaOptions> kafkaOptions,
        ILogger<MetricsIngestionWorker> logger,
        Instrumentation instrumentation)
        : base(kafkaOptions, logger, instrumentation)
    {
        this.metricsStore = metricsStore;
        this.hubContext = hubContext;
        this.kafkaOptions = kafkaOptions.Value;
    }

    protected override string Topic => kafkaOptions.MetricsTopic;

    protected override string ConsumerGroupId => kafkaOptions.MetricsConsumerGroup;

    protected override async Task HandleAsync(EndpointMetrics message, CancellationToken cancellationToken)
    {
        metricsStore.Record(message);

        await hubContext.Clients
            .Group(message.EndpointId.ToString())
            .SendAsync("MetricUpdated", message, cancellationToken);
    }
}
