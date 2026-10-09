using backend.Services;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers;

[ApiController]
[Route("api/workflows")]
public sealed class WorkflowsController : ControllerBase
{
    private readonly IncidentWorkflowService _workflowService;

    public WorkflowsController(IncidentWorkflowService workflowService)
    {
        _workflowService = workflowService;
    }

    [HttpGet]
    public async Task<IActionResult> GetRecent(
        [FromQuery] int limit = 100,
        CancellationToken cancellationToken = default)
    {
        var workflows = await _workflowService.GetRecentAsync(limit, cancellationToken);
        return Ok(workflows);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var workflow = await _workflowService.GetByIdAsync(id, cancellationToken);
        return workflow is null ? NotFound() : Ok(workflow);
    }

    [HttpPost("{id:guid}/retry")]
    public async Task<IActionResult> Retry(
        Guid id,
        CancellationToken cancellationToken)
    {
        var workflow = await _workflowService.RetryAsync(id, cancellationToken);
        if (workflow is null)
        {
            return NotFound();
        }

        return string.Equals(workflow.Status, "Completed", StringComparison.Ordinal)
            ? Ok(workflow)
            : Conflict(workflow);
    }
}
