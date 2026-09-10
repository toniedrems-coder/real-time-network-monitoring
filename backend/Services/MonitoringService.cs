using System.Collections.Concurrent;
using System.Diagnostics;
using backend.Models;
using backend.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;

namespace backend.Services;

public sealed class MonitoringService : BackgroundService
{
    private static readonly string[] SeedHosts = ["jumia.com","www.mtn.ng", "google.com", "mtn.com.ng"];
    private static readonly TimeSpan ProbeInterval = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(10);
    private readonly EndpointService endpointService;
    private readonly IHttpClientFactory httpClientFactory;
    private readonly ILogger<MonitoringService> logger;
    private readonly IHubContext<MetricsHub> hubContext;
    private readonly ConcurrentDictionary<Guid, EndpointMetrics> latestMetrics = new();
    private readonly ConcurrentDictionary<Guid, ConcurrentQueue<EndpointMetrics>> history = new();

    public MonitoringService(
        EndpointService endpointService,
        IHttpClientFactory httpClientFactory,
        ILogger<MonitoringService> logger,
        IHubContext<MetricsHub> hubContext)
    {
        this.endpointService = endpointService;
        this.httpClientFactory = httpClientFactory;
        this.logger = logger;
        this.hubContext = hubContext;
    }

    public EndpointMetrics? GetLatest(Guid endpointId)
    {
        return latestMetrics.TryGetValue(endpointId, out var metrics) ? metrics : null;
    }

    public IReadOnlyList<EndpointMetrics> GetHistory(Guid endpointId)
    {
        return history.TryGetValue(endpointId, out var metrics)
            ? metrics.ToArray()
            : [];
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

        var metrics = new EndpointMetrics(
            endpoint.Id,
            timestamp,
            Math.Round(stopwatch.Elapsed.TotalMilliseconds, 2),
            reachable ? 0 : 100,
            reachable ? 100 : 0,
            reachable ? 0 : 100,
            Math.Round(throughputMbps, 4),
            reachable);

        latestMetrics[endpoint.Id] = metrics;
        var endpointHistory = history.GetOrAdd(endpoint.Id, _ => new ConcurrentQueue<EndpointMetrics>());
        endpointHistory.Enqueue(metrics);
        while (endpointHistory.Count > 100)
        {
            endpointHistory.TryDequeue(out _);
        }

        await hubContext.Clients
            .Group(endpoint.Id.ToString())
            .SendAsync("MetricUpdated", metrics, cancellationToken);
    }
}