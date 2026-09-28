using backend.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace backend.Realtime;

public sealed class SignalRAiOpsEventPublisher
    : IAiOpsEventPublisher
{
    private readonly IHubContext<AiOpsHub> hubContext;

    public SignalRAiOpsEventPublisher(
        IHubContext<AiOpsHub> hubContext)
    {
        this.hubContext = hubContext;
    }

    public Task AgentStartedAsync(
        AgentEvent @event,
        CancellationToken cancellationToken = default)
    {
        return SendAsync(
            "AgentStarted",
            @event,
            cancellationToken);
    }

    public Task AgentCompletedAsync(
        AgentEvent @event,
        CancellationToken cancellationToken = default)
    {
        return SendAsync(
            "AgentCompleted",
            @event,
            cancellationToken);
    }

    public Task AgentFailedAsync(
        AgentEvent @event,
        CancellationToken cancellationToken = default)
    {
        return SendAsync(
            "AgentFailed",
            @event,
            cancellationToken);
    }

    public Task IncidentCreatedAsync(
        IncidentEvent @event,
        CancellationToken cancellationToken = default)
    {
        return SendAsync(
            "IncidentCreated",
            @event,
            cancellationToken);
    }

    public Task IncidentUpdatedAsync(
        IncidentEvent @event,
        CancellationToken cancellationToken = default)
    {
        return SendAsync(
            "IncidentUpdated",
            @event,
            cancellationToken);
    }

    public Task IncidentResolvedAsync(
        IncidentEvent @event,
        CancellationToken cancellationToken = default)
    {
        return SendAsync(
            "IncidentResolved",
            @event,
            cancellationToken);
    }

    public Task RcaStartedAsync(
        RcaEvent @event,
        CancellationToken cancellationToken = default)
    {
        return SendAsync(
            "RcaStarted",
            @event,
            cancellationToken);
    }

    public Task RcaCompletedAsync(
        RcaEvent @event,
        CancellationToken cancellationToken = default)
    {
        return SendAsync(
            "RcaCompleted",
            @event,
            cancellationToken);
    }

    public Task RcaFailedAsync(
        RcaEvent @event,
        CancellationToken cancellationToken = default)
    {
        return SendAsync(
            "RcaFailed",
            @event,
            cancellationToken);
    }

    private Task SendAsync<T>(
        string eventName,
        T payload,
        CancellationToken cancellationToken)
    {
        return hubContext.Clients.All.SendAsync(
            eventName,
            payload,
            cancellationToken);
    }
}