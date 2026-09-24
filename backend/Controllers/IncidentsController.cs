using backend.Models;
using backend.Services;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers;

[ApiController]
[Route("api/incidents")]
public sealed class IncidentsController : ControllerBase
{
    private readonly IncidentService incidentService;

    public IncidentsController(
        IncidentService incidentService)
    {
        this.incidentService = incidentService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        CancellationToken cancellationToken)
    {
        var incidents =
            await incidentService.GetAllAsync(
                cancellationToken);

        return Ok(incidents);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var incident =
            await incidentService.GetByIdAsync(
                id,
                cancellationToken);

        return incident is null
            ? NotFound()
            : Ok(incident);
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateIncidentRequest request,
        CancellationToken cancellationToken)
    {
        var incident =
            await incidentService.CreateOrUpdateAsync(
                request,
                cancellationToken);

        return Ok(incident);
    }

    [HttpPost("{id:guid}/resolve")]
    public async Task<IActionResult> Resolve(
        Guid id,
        [FromBody] ResolveIncidentRequest request,
        CancellationToken cancellationToken)
    {
        var incident =
            await incidentService.ResolveAsync(
                id,
                request.Resolution,
                cancellationToken);

        return incident is null
            ? NotFound()
            : Ok(incident);
    }
}

public sealed record ResolveIncidentRequest(
    string Resolution);

    