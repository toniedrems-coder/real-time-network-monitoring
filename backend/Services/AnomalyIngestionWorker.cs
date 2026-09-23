using backend.Data;
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
    private readonly IServiceScopeFactory scopeFactory;

    public AnomalyIngestionWorker(
        AnomalyStore anomalyStore,
        IHubContext<MetricsHub> hubContext,
        IOptions<KafkaOptions> kafkaOptions,
        ILogger<AnomalyIngestionWorker> logger,
        IServiceScopeFactory scopeFactory,
        Instrumentation instrumentation)
        : base(kafkaOptions, logger, instrumentation)
    {
        this.anomalyStore = anomalyStore;
        this.hubContext = hubContext;
        this.kafkaOptions = kafkaOptions.Value;
        this.scopeFactory = scopeFactory;
    }

    protected override string Topic => kafkaOptions.AnomaliesTopic;

    protected override string ConsumerGroupId => "network-monitor-anomaly-ingestion";

    protected override async Task HandleAsync(
    DetectedAnomaly message,
    CancellationToken cancellationToken)
    {
        // 1. Persist durable anomaly history.
        using (var scope = scopeFactory.CreateScope())
        {
            var repository =
                scope.ServiceProvider.GetRequiredService<IAnomalyRepository>();

            await repository.AddAsync(message, cancellationToken);
        }

        // 2. Keep recent anomaly in memory.
        anomalyStore.Record(message);

        // 3. Push live anomaly to dashboard clients.
        await hubContext.Clients
            .Group(message.EndpointId.ToString())
            .SendAsync(
                "AnomalyDetected",
                message,
                cancellationToken);
    }
}
