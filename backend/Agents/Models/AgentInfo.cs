namespace backend.Agents.Models;

public class AgentInfo
{
    public string Id { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public AgentStatus Status { get; set; }

    public bool Enabled { get; set; }

    public DateTimeOffset? LastExecutionAt { get; set; }

    public DateTimeOffset? LastSuccessfulExecutionAt { get; set; }

    public string? LastMessage { get; set; }
}