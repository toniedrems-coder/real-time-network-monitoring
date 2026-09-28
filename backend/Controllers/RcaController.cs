using backend.Agents.Abstractions;
using backend.Agents.Core;
using backend.Agents.Models;
using backend.Agents.Rca.Storage;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers;

[ApiController]
[Route("api/rca")]
public sealed class RcaController : ControllerBase
{
    private readonly IRcaResultStore rcaResultStore;
    private readonly IAgentOrchestrator orchestrator;

    public RcaController(
        IRcaResultStore rcaResultStore,
        IAgentOrchestrator orchestrator)
    {
        this.rcaResultStore = rcaResultStore;
        this.orchestrator = orchestrator;
    }

    [HttpGet("{incidentId:guid}")]
    public IActionResult Get(
        Guid incidentId)
    {
        var analysis =
            rcaResultStore.GetByIncidentId(
                incidentId);

        if (analysis is null)
        {
            return NotFound(
                new
                {
                    message =
                        "No RCA analysis has been generated for this incident."
                });
        }

        return Ok(analysis);
    }

    [HttpPost("{incidentId:guid}/analyze")]
    public async Task<IActionResult> Analyze(
        Guid incidentId,
        CancellationToken cancellationToken)
    {
        var context = new AgentContext
        {
            IncidentId =
                incidentId.ToString(),

            TargetType =
                "Incident"
        };

        var result =
            await orchestrator.ExecuteAgentAsync(
                "rca-agent",
                context,
                cancellationToken);

        return Ok(result);
    }
}