using backend.Models;
using backend.Services;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers;

[ApiController]
[Route("anomalies")]
public sealed class AnomaliesController : ControllerBase
{
    private readonly EndpointService endpointService;
    private readonly AnomalyStore anomalyStore;

    public AnomaliesController(
        EndpointService endpointService,
        AnomalyStore anomalyStore)
    {
        this.endpointService = endpointService;
        this.anomalyStore = anomalyStore;
    }

    [HttpGet]
    public ActionResult<IReadOnlyList<DetectedAnomaly>> GetAll()
    {
        return Ok(anomalyStore.GetAll());
    }

    [HttpGet("{endpointId:guid}")]
    public ActionResult<IReadOnlyList<DetectedAnomaly>> GetForEndpoint(Guid endpointId)
    {
        if (endpointService.GetById(endpointId) is null)
        {
            return NotFound();
        }

        return Ok(anomalyStore.GetForEndpoint(endpointId));
    }
}