using backend.Agents.Abstractions;
using backend.Agents.Core;
using backend.Agents.Models;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers;

[ApiController]
[Route("api/agents")]
public class AgentsController : ControllerBase
{
    private readonly AgentRegistry _registry;
    private readonly IAgentOrchestrator _orchestrator;

    public AgentsController(
        AgentRegistry registry,
        IAgentOrchestrator orchestrator)
    {
        _registry = registry;
        _orchestrator = orchestrator;
    }

    [HttpGet]
    public IActionResult GetAgents()
    {
        return Ok(_registry.GetAgentInfo());
    }

    [HttpGet("{id}")]
    public IActionResult GetAgent(string id)
    {
        var agent = _registry.GetById(id);

        if (agent is null)
            return NotFound();

        return Ok(new AgentInfo
        {
            Id = agent.Id,
            Name = agent.Name,
            Description = agent.Description,
            Status = agent.Status,
            Enabled = agent.Enabled
        });
    }

    [HttpPost("{id}/execute")]
    public async Task<IActionResult> Execute(
        string id,
        [FromBody] AgentContext? context,
        CancellationToken cancellationToken)
    {
        var agent = _registry.GetById(id);

        if (agent is null)
            return NotFound();

        var result = await _orchestrator.ExecuteAgentAsync(
            id,
            context ?? new AgentContext(),
            cancellationToken);

        return Ok(result);
    }
}