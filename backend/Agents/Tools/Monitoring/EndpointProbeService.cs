using System.Diagnostics;
using backend.Agents.Rca.Logging;
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
    private readonly InMemoryLogSearchService logSearchService;

    public EndpointProbeService(
        IHttpClientFactory httpClientFactory,
        EndpointService endpointService,
        Instrumentation instrumentation,
        ILogger<EndpointProbeService> logger,
        InMemoryLogSearchService logSearchService)
    {
        this.httpClientFactory = httpClientFactory;
        this.endpointService = endpointService;
        this.instrumentation = instrumentation;
        this.logger = logger;
        this.logSearchService = logSearchService;
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
        var status = string.Empty;
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
                Math.Max(
                    stopwatch.Elapsed.TotalSeconds,
                    0.001) /
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

            timestamp = DateTimeOffset.UtcNow;
            status = "Unavailable";
            errorMessage = exception.Message;

            logSearchService.Add(
                new LogEntry
                {
                    Timestamp = timestamp,
                    Level = "Error",
                    Source = "EndpointProbeService",
                    Target = endpoint.Url,
                    Message = errorMessage,
                    ExceptionType =
                        exception.GetType().Name
                });

            logger.LogWarning(
                exception,
                "Probe failed for endpoint {EndpointId} ({Url}).",
                endpoint.Id,
                endpoint.Url);

            await endpointService.UpdateStatusAsync(
                endpoint.Id,
                status,
                cancellationToken);
        }
        catch (TaskCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            stopwatch.Stop();

            logger.LogInformation(
                "Probe cancelled for endpoint {EndpointId} ({Url}).",
                endpoint.Id,
                endpoint.Url);

            throw;
        }
        catch (TaskCanceledException exception)
        {
            stopwatch.Stop();

            timestamp = DateTimeOffset.UtcNow;
            status = "Timeout";
            errorMessage = "Endpoint probe timed out.";

            logSearchService.Add(
                new LogEntry
                {
                    Timestamp = timestamp,
                    Level = "Error",
                    Source = "EndpointProbeService",
                    Target = endpoint.Url,
                    Message = errorMessage,
                    ExceptionType =
                        exception.GetType().Name
                });

            logger.LogWarning(
                exception,
                "Probe timed out for endpoint {EndpointId} ({Url}).",
                endpoint.Id,
                endpoint.Url);

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
