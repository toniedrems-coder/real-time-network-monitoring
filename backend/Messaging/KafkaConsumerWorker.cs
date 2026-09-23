using System.Text.Json;
using backend.Observability;
using Confluent.Kafka;
using Microsoft.Extensions.Options;

namespace backend.Messaging;

/// <summary>
/// Generic Kafka consumer background worker. Subclasses provide the topic, group id,
/// and per-message handling logic; this base class owns the consume loop, offset
/// commits, and resilient error handling so message consumption keeps running.
/// </summary>
public abstract class KafkaConsumerWorker<TValue> : BackgroundService
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly KafkaOptions kafkaOptions;
    private readonly ILogger logger;
    private readonly Instrumentation instrumentation;

    protected KafkaConsumerWorker(IOptions<KafkaOptions> kafkaOptions, ILogger logger, Instrumentation instrumentation)
    {
        this.kafkaOptions = kafkaOptions.Value;
        this.logger = logger;
        this.instrumentation = instrumentation;
    }

    protected abstract string Topic { get; }

    protected abstract string ConsumerGroupId { get; }

    protected abstract Task HandleAsync(TValue message, CancellationToken cancellationToken);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var config = new ConsumerConfig
        {
            BootstrapServers = kafkaOptions.BootstrapServers,
            GroupId = ConsumerGroupId,
            AutoOffsetReset = AutoOffsetReset.Latest,
           // EnableAutoCommit = true,
            EnableAutoCommit = false,
            EnableAutoOffsetStore = false,
        };

        using var consumer = new ConsumerBuilder<string, string>(config).Build();
        consumer.Subscribe(Topic);

        await Task.Yield();

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var result = consumer.Consume(stoppingToken);
                if (result?.Message is null)
                {
                    continue;
                }

                var value = JsonSerializer.Deserialize<TValue>(result.Message.Value, SerializerOptions);
                if (value is not null)
                {
                    instrumentation.KafkaConsumedCounter.Add(1, new KeyValuePair<string, object?>("topic", Topic));
                    await HandleAsync(value, stoppingToken);

                    consumer.StoreOffset(result);
                    consumer.Commit(result);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (ConsumeException exception)
            {
                logger.LogError(exception, "Kafka consume error on topic {Topic}.", Topic);
                await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Unhandled error processing message from topic {Topic}.", Topic);
            }
        }

        consumer.Close();
    }
}
