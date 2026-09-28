namespace backend.Realtime;

public interface IAiOpsEventPublisher
{
    Task AgentStartedAsync(
        AgentEvent @event,
        CancellationToken cancellationToken = default);

    Task AgentCompletedAsync(
        AgentEvent @event,
        CancellationToken cancellationToken = default);

    Task AgentFailedAsync(
        AgentEvent @event,
        CancellationToken cancellationToken = default);

    Task IncidentCreatedAsync(
        IncidentEvent @event,
        CancellationToken cancellationToken = default);

    Task IncidentUpdatedAsync(
        IncidentEvent @event,
        CancellationToken cancellationToken = default);

    Task IncidentResolvedAsync(
        IncidentEvent @event,
        CancellationToken cancellationToken = default);

    Task RcaStartedAsync(
        RcaEvent @event,
        CancellationToken cancellationToken = default);

    Task RcaCompletedAsync(
        RcaEvent @event,
        CancellationToken cancellationToken = default);

    Task RcaFailedAsync(
        RcaEvent @event,
        CancellationToken cancellationToken = default);
}