using backend.Data;
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
    private readonly IAnomalyRepository anomalyRepository;

    public AnomaliesController(
        EndpointService endpointService,
        AnomalyStore anomalyStore,
        IAnomalyRepository anomalyRepository)
    {
        this.endpointService = endpointService;
        this.anomalyStore = anomalyStore;
        this.anomalyRepository = anomalyRepository;
    }

    /// <summary>
    /// Returns recent anomalies currently held in memory.
    /// Useful for the live dashboard.
    /// </summary>
    [HttpGet]
    public ActionResult<IReadOnlyList<DetectedAnomaly>> GetAll()
    {
        return Ok(anomalyStore.GetAll());
    }

    /// <summary>
    /// Returns durable anomaly history for an endpoint.
    /// </summary>
    [HttpGet("{endpointId:guid}")]
    public async Task<ActionResult<IReadOnlyList<DetectedAnomaly>>>
        GetForEndpoint(
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
            return BadRequest(
                "'from' must be earlier than or equal to 'to'.");
        }

        var anomalies =
            await anomalyRepository.GetForEndpointAsync(
                endpointId,
                from,
                to,
                cancellationToken);

        return Ok(anomalies);
    }
}