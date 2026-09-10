using System.Diagnostics;
using backend.Messaging;
using backend.Models;
using backend.Observability;
using Microsoft.Extensions.Options;

namespace backend.Services;

/// <summary>
/// Producer-side worker: probes endpoints on a fixed interval and publishes the
/// resulting KPIs to Kafka (<see cref="KafkaOptions.MetricsTopic"/>) for downstream
/// consumers (metrics ingestion, anomaly detection) instead of mutating shared state directly.
/// </summary>
public sealed class MonitoringService : BackgroundService
{
    private static readonly string[] SeedHosts = ["jumia.com","www.mtn.ng", "google.com", "mtn.com.ng"];
    private static readonly TimeSpan ProbeInterval = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(10);
    private readonly EndpointService endpointService;
    private readonly IHttpClientFactory httpClientFactory;
    private readonly ILogger<MonitoringService> logger;
    private readonly KafkaProducerService kafkaProducer;
    private readonly string metricsTopic;
    private readonly Instrumentation instrumentation;

    public MonitoringService(
        EndpointService endpointService,
        IHttpClientFactory httpClientFactory,
        ILogger<MonitoringService> logger,
        KafkaProducerService kafkaProducer,
        IOptions<KafkaOptions> kafkaOptions,
        Instrumentation instrumentation)
    {
        this.endpointService = endpointService;
        this.httpClientFactory = httpClientFactory;
        this.logger = logger;
        this.kafkaProducer = kafkaProducer;
        metricsTopic = kafkaOptions.Value.MetricsTopic;
        this.instrumentation = instrumentation;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await endpointService.DiscoverAsync(SeedHosts, stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            await ProbeAllAsync(stoppingToken);
            await Task.Delay(ProbeInterval, stoppingToken);
        }
    }

    private async Task ProbeAllAsync(CancellationToken cancellationToken)
    {
        var endpoints = endpointService.GetAll();
        await Task.WhenAll(endpoints.Select(endpoint => ProbeAsync(endpoint, cancellationToken)));
    }

    private async Task ProbeAsync(MonitoredEndpoint endpoint, CancellationToken cancellationToken)
    {
        var timestamp = DateTimeOffset.UtcNow;
        var stopwatch = Stopwatch.StartNew();
        var reachable = false;
        var throughputMbps = 0d;

        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(RequestTimeout);
            using var response = await httpClientFactory.CreateClient().GetAsync(
                endpoint.Url,
                HttpCompletionOption.ResponseHeadersRead,
                timeout.Token);
            var body = await response.Content.ReadAsByteArrayAsync(timeout.Token);
            stopwatch.Stop();
            reachable = response.IsSuccessStatusCode;
            throughputMbps = body.Length * 8d / stopwatch.Elapsed.TotalSeconds / 1_000_000d;
            endpointService.UpdateStatus(
                endpoint.Id,
                reachable ? $"Healthy ({(int)response.StatusCode})" : $"Unhealthy ({(int)response.StatusCode})");
        }
        catch (HttpRequestException exception)
        {
            stopwatch.Stop();
            logger.LogWarning(exception, "Probe failed for endpoint {EndpointId}.", endpoint.Id);
            endpointService.UpdateStatus(endpoint.Id, "Unavailable");
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            stopwatch.Stop();
            logger.LogWarning("Probe timed out for endpoint {EndpointId}.", endpoint.Id);
            endpointService.UpdateStatus(endpoint.Id, "Timeout");
        }

        instrumentation.ProbeLatencyHistogram.Record(
            stopwatch.Elapsed.TotalMilliseconds,
            new KeyValuePair<string, object?>("endpointId", endpoint.Id.ToString()));
        instrumentation.ProbeResultCounter.Add(
            1,
            new KeyValuePair<string, object?>("reachable", reachable));

        var metrics = new EndpointMetrics(
            endpoint.Id,
            timestamp,
            Math.Round(stopwatch.Elapsed.TotalMilliseconds, 2),
            reachable ? 0 : 100,
            reachable ? 100 : 0,
            reachable ? 0 : 100,
            Math.Round(throughputMbps, 4),
            reachable);

        await kafkaProducer.PublishAsync(metricsTopic, endpoint.Id.ToString(), metrics, cancellationToken);
    }
}