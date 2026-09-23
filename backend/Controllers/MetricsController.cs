using backend.Data;
using backend.Models;
using backend.Services;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers;

[ApiController]
[Route("metrics")]
public sealed class MetricsController : ControllerBase
{
    private readonly EndpointService endpointService;
    private readonly MetricsStore metricsStore;
    private readonly IMetricsRepository metricsRepository;

    public MetricsController(
        EndpointService endpointService,
        MetricsStore metricsStore,
        IMetricsRepository metricsRepository)
    {
        this.endpointService = endpointService;
        this.metricsStore = metricsStore;
        this.metricsRepository = metricsRepository;
    }

    /// <summary>
    /// Returns the latest in-memory metric for an endpoint.
    /// Intended for current/live status.
    /// </summary>
    [HttpGet("{endpointId:guid}")]
    public ActionResult<EndpointMetrics> GetMetrics(Guid endpointId)
    {
        if (endpointService.GetById(endpointId) is null)
        {
            return NotFound();
        }

        var metrics = metricsStore.GetLatest(endpointId);

        return metrics is null
            ? StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                "Metrics are not available yet.")
            : Ok(metrics);
    }

    /// <summary>
    /// Returns durable historical metrics from PostgreSQL.
    /// Optional from/to parameters can restrict the time range.
    /// </summary>
    [HttpGet("trends/{endpointId:guid}")]
    public async Task<ActionResult<IReadOnlyList<MetricTrendPoint>>> GetTrends(
        Guid endpointId,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        CancellationToken cancellationToken)
    {
        if (endpointService.GetById(endpointId) is null)
        {
            return NotFound();
        }

        if (from.HasValue &&
            to.HasValue &&
            from.Value > to.Value)
        {
            return BadRequest("'from' must be earlier than or equal to 'to'.");
        }

        var history =
            await metricsRepository.GetHistoryAsync(
                endpointId,
                from,
                to,
                cancellationToken);

        var trends = history
            .Select(metrics => new MetricTrendPoint(
                metrics.Timestamp,
                metrics.LatencyMs,
                metrics.AvailabilityPercent,
                metrics.ErrorRatePercent,
                metrics.ThroughputMbps))
            .ToArray();

        return Ok(trends);
    }
}