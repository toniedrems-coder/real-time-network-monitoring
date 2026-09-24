using backend.Agents.Models;

namespace backend.Agents.Abstractions;

public interface IAiOpsAgent
{
    string Id { get; }

    string Name { get; }

    string Description { get; }

    AgentStatus Status { get; }

    bool Enabled { get; }

    Task<AgentResult> ExecuteAsync(
        AgentContext context,
        CancellationToken cancellationToken = default);
}


// IAiOpsAgent
//    │
//    ├── MonitoringAgent
//    ├── DiscoveryAgent
//    ├── LogAnalysisAgent
//    ├── AnomalyAgent
//    ├── RootCauseAnalysisAgent
//    ├── KnowledgeAgent
//    ├── RemediationAgent
//    ├── NotificationAgent
//    ├── IncidentAgent
//    └── VerificationAgent