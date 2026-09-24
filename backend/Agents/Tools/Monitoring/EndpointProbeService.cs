using System.Diagnostics;
using backend.Models;
using backend.Observability;
using backend.Services;

namespace backend.Agents.Tools.Monitoring;

public sealed class EndpointProbeService : IEndpointProbeService
{
    private static readonly TimeSpan RequestTimeout =
        TimeSpan.FromSeconds(10);

    private readonly IHttpClientFactory httpClientFactory;
    private readonly EndpointService endpointService;
    private readonly Instrumentation instrumentation;
    private readonly ILogger<EndpointProbeService> logger;

    public EndpointProbeService(
        IHttpClientFactory httpClientFactory,
        EndpointService endpointService,
        Instrumentation instrumentation,
        ILogger<EndpointProbeService> logger)
    {
        this.httpClientFactory = httpClientFactory;
        this.endpointService = endpointService;
        this.instrumentation = instrumentation;
        this.logger = logger;
    }

    public async Task<EndpointProbeResult> ProbeAsync(
        MonitoredEndpoint endpoint,
        CancellationToken cancellationToken = default)
    {
        var timestamp = DateTimeOffset.UtcNow;
        var stopwatch = Stopwatch.StartNew();

        var reachable = false;
        var throughputMbps = 0d;
        int? statusCode = null;
        string status;
        string? errorMessage = null;

        try
        {
            using var timeout =
                CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken);

            timeout.CancelAfter(RequestTimeout);

            using var response =
                await httpClientFactory
                    .CreateClient()
                    .GetAsync(
                        endpoint.Url,
                        HttpCompletionOption.ResponseHeadersRead,
                        timeout.Token);

            var body =
                await response.Content.ReadAsByteArrayAsync(
                    timeout.Token);

            stopwatch.Stop();

            statusCode = (int)response.StatusCode;
            reachable = response.IsSuccessStatusCode;

            throughputMbps =
                body.Length * 8d /
                Math.Max(stopwatch.Elapsed.TotalSeconds, 0.001) /
                1_000_000d;

            status = reachable
                ? $"Healthy ({statusCode})"
                : $"Unhealthy ({statusCode})";

            await endpointService.UpdateStatusAsync(
                endpoint.Id,
                status,
                cancellationToken);
        }
        catch (HttpRequestException exception)
        {
            stopwatch.Stop();

            status = "Unavailable";
            errorMessage = exception.Message;

            logger.LogWarning(
                exception,
                "Probe failed for endpoint {EndpointId}.",
                endpoint.Id);

            await endpointService.UpdateStatusAsync(
                endpoint.Id,
                status,
                cancellationToken);
        }
        catch (TaskCanceledException)
            when (!cancellationToken.IsCancellationRequested)
        {
            stopwatch.Stop();

            status = "Timeout";
            errorMessage = "Endpoint probe timed out.";

            logger.LogWarning(
                "Probe timed out for endpoint {EndpointId}.",
                endpoint.Id);

            await endpointService.UpdateStatusAsync(
                endpoint.Id,
                status,
                cancellationToken);
        }

        instrumentation.ProbeLatencyHistogram.Record(
            stopwatch.Elapsed.TotalMilliseconds,
            new KeyValuePair<string, object?>(
                "endpointId",
                endpoint.Id.ToString()));

        instrumentation.ProbeResultCounter.Add(
            1,
            new KeyValuePair<string, object?>(
                "reachable",
                reachable));

        return new EndpointProbeResult(
            endpoint.Id,
            endpoint.Url,
            timestamp,
            reachable,
            statusCode,
            Math.Round(
                stopwatch.Elapsed.TotalMilliseconds,
                2),
            Math.Round(
                throughputMbps,
                4),
            status,
            errorMessage);
    }
}