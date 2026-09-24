namespace backend.Agents.Models;

public class AgentResult
{
    public bool Success { get; init; }

    public string AgentName { get; init; } = string.Empty;

    public string Message { get; init; } = string.Empty;

    public DateTimeOffset StartedAt { get; init; }

    public DateTimeOffset CompletedAt { get; init; }

    public Dictionary<string, object> Data { get; init; } = new();

    public static AgentResult Successful(
        string agentName,
        string message,
        DateTimeOffset startedAt,
        Dictionary<string, object>? data = null)
    {
        return new AgentResult
        {
            Success = true,
            AgentName = agentName,
            Message = message,
            StartedAt = startedAt,
            CompletedAt = DateTimeOffset.UtcNow,
            Data = data ?? new Dictionary<string, object>()
        };
    }

    public static AgentResult Failed(
        string agentName,
        string message,
        DateTimeOffset startedAt)
    {
        return new AgentResult
        {
            Success = false,
            AgentName = agentName,
            Message = message,
            StartedAt = startedAt,
            CompletedAt = DateTimeOffset.UtcNow
        };
    }
}