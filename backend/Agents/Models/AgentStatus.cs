namespace backend.Agents.Models;

public enum AgentStatus
{
    Stopped,
    Starting,
    Running,
    Investigating,
    Executing,
    Failed,
    Disabled
}