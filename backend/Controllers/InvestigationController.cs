using backend.Agents.Abstractions;
using backend.Agents.Models;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers;

[ApiController]
[Route("api/investigations")]
public sealed class InvestigationController : ControllerBase
{
    private readonly IAgentOrchestrator _orchestrator;

    public InvestigationController(
        IAgentOrchestrator orchestrator)
    {
        _orchestrator =
            orchestrator;
    }

    [HttpPost("{incidentId:guid}/execute")]
    public async Task<IActionResult> Execute(
        Guid incidentId,
        CancellationToken cancellationToken)
    {
        var context =
            new AgentContext
            {
                IncidentId =
                    incidentId.ToString(),

                TargetType =
                    "Incident"
            };

        var result =
            await _orchestrator
                .ExecuteInvestigationAsync(
                    context,
                    cancellationToken);

        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }
}