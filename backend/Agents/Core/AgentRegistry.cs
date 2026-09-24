using backend.Agents.Abstractions;
using backend.Agents.Models;

namespace backend.Agents.Core;

public class AgentRegistry
{
    private readonly IReadOnlyDictionary<string, IAiOpsAgent> _agents;

    public AgentRegistry(IEnumerable<IAiOpsAgent> agents)
    {
        _agents = agents.ToDictionary(
            agent => agent.Id,
            StringComparer.OrdinalIgnoreCase);
    }

    public IEnumerable<IAiOpsAgent> GetAll()
    {
        return _agents.Values;
    }

    public IAiOpsAgent? GetById(string id)
    {
        return _agents.GetValueOrDefault(id);
    }

    public IEnumerable<AgentInfo> GetAgentInfo()
    {
        return _agents.Values.Select(agent => new AgentInfo
        {
            Id = agent.Id,
            Name = agent.Name,
            Description = agent.Description,
            Status = agent.Status,
            Enabled = agent.Enabled
        });
    }
}