using backend.Models;
using backend.Services;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers;

[ApiController]
[Route("metrics")]
public sealed class MetricsController : ControllerBase
{
    private readonly EndpointService endpointService;
    private readonly MonitoringService monitoringService;

    public MetricsController(
        EndpointService endpointService,
        MonitoringService monitoringService)
    {
        this.endpointService = endpointService;
        this.monitoringService = monitoringService;
    }

    [HttpGet("{endpointId:guid}")]
    public ActionResult<EndpointMetrics> GetMetrics(Guid endpointId)
    {
        if (endpointService.GetById(endpointId) is null)
        {
            return NotFound();
        }

        var metrics = monitoringService.GetLatest(endpointId);
        return metrics is null
            ? StatusCode(StatusCodes.Status503ServiceUnavailable, "Metrics are not available yet.")
            : Ok(metrics);
    }

    [HttpGet("trends/{endpointId:guid}")]
    public ActionResult<IReadOnlyList<MetricTrendPoint>> GetTrends(Guid endpointId)
    {
        if (endpointService.GetById(endpointId) is null)
        {
            return NotFound();
        }

        var trends = monitoringService.GetHistory(endpointId)
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