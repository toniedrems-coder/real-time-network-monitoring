using System.Text.Json;
using backend.Observability;
using Confluent.Kafka;
using Microsoft.Extensions.Options;

namespace backend.Messaging;

/// <summary>
/// Thin wrapper around a Confluent.Kafka producer. Registered as a singleton so the
/// underlying librdkafka client/connection pool is reused across the app lifetime.
/// </summary>
public sealed class KafkaProducerService : IAsyncDisposable
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly IProducer<string, string> producer;
    private readonly ILogger<KafkaProducerService> logger;
    private readonly Instrumentation instrumentation;

    public KafkaProducerService(IOptions<KafkaOptions> options, ILogger<KafkaProducerService> logger, Instrumentation instrumentation)
    {
        this.logger = logger;
        this.instrumentation = instrumentation;
        var config = new ProducerConfig
        {
            BootstrapServers = options.Value.BootstrapServers,
            Acks = Acks.Leader,
            MessageSendMaxRetries = 3,
            EnableIdempotence = false
        };

        producer = new ProducerBuilder<string, string>(config).Build();
    }

    public async Task PublishAsync<T>(string topic, string key, T value, CancellationToken cancellationToken = default)
    {
        var payload = JsonSerializer.Serialize(value, SerializerOptions);
        try
        {
            await producer.ProduceAsync(
                topic,
                new Message<string, string> { Key = key, Value = payload },
                cancellationToken);
            instrumentation.KafkaProducedCounter.Add(1, new KeyValuePair<string, object?>("topic", topic));
        }
        catch (ProduceException<string, string> exception)
        {
            logger.LogError(exception, "Failed to publish message to Kafka topic {Topic}.", topic);
            throw;
        }
    }

    public ValueTask DisposeAsync()
    {
        producer.Flush(TimeSpan.FromSeconds(5));
        producer.Dispose();
        return ValueTask.CompletedTask;
    }
}
