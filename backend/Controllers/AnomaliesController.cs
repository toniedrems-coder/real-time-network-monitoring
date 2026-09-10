using backend.Models;
using backend.Services;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers;

[ApiController]
[Route("anomalies")]
public sealed class AnomaliesController : ControllerBase
{
    private readonly EndpointService endpointService;
    private readonly MonitoringService monitoringService;
    private readonly AnomalyDetectionService anomalyDetectionService;

    public AnomaliesController(
        EndpointService endpointService,
        MonitoringService monitoringService,
        AnomalyDetectionService anomalyDetectionService)
    {
        this.endpointService = endpointService;
        this.monitoringService = monitoringService;
        this.anomalyDetectionService = anomalyDetectionService;
    }

    [HttpGet]
    public ActionResult<IReadOnlyList<DetectedAnomaly>> GetAll()
    {
        var anomalies = endpointService.GetAll()
            .SelectMany(endpoint => anomalyDetectionService.Detect(
                endpoint.Id,
                monitoringService.GetHistory(endpoint.Id)))
            .OrderByDescending(anomaly => anomaly.DetectedAt)
            .ToArray();

        return Ok(anomalies);
    }

    [HttpGet("{endpointId:guid}")]
    public ActionResult<IReadOnlyList<DetectedAnomaly>> GetForEndpoint(Guid endpointId)
    {
        if (endpointService.GetById(endpointId) is null)
        {
            return NotFound();
        }

        return Ok(anomalyDetectionService.Detect(
            endpointId,
            monitoringService.GetHistory(endpointId)));
    }
}