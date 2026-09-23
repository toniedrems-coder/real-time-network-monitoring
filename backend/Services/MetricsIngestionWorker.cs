using backend.Data;
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

    private readonly IServiceScopeFactory scopeFactory;

    public MetricsIngestionWorker(
        MetricsStore metricsStore,
        IHubContext<MetricsHub> hubContext,
        IOptions<KafkaOptions> kafkaOptions,
        ILogger<MetricsIngestionWorker> logger,
        Instrumentation instrumentation,
        IServiceScopeFactory scopeFactory)
        : base(kafkaOptions, logger, instrumentation)
    {
        this.metricsStore = metricsStore;
        this.hubContext = hubContext;
        this.kafkaOptions = kafkaOptions.Value;
        this.scopeFactory = scopeFactory;
    }

    protected override string Topic => kafkaOptions.MetricsTopic;

    protected override string ConsumerGroupId => kafkaOptions.MetricsConsumerGroup;

    protected override async Task HandleAsync(
    EndpointMetrics message,
    CancellationToken cancellationToken)
    {
        // 1. Persist durable history first.
        using (var scope = scopeFactory.CreateScope())
        {
            var repository =
                scope.ServiceProvider.GetRequiredService<IMetricsRepository>();

            await repository.AddAsync(message, cancellationToken);
        }

        // 2. Update fast in-memory state.
        metricsStore.Record(message);

        // 3. Push live update to connected dashboard clients.
        await hubContext.Clients
            .Group(message.EndpointId.ToString())
            .SendAsync(
                "MetricUpdated",
                message,
                cancellationToken);
    }
}
