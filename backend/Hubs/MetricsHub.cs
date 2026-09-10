using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Authorization;

namespace backend.Hubs;

[Authorize]
public sealed class MetricsHub : Hub
{
    public Task SubscribeToEndpoint(Guid endpointId)
    {
        return Groups.AddToGroupAsync(Context.ConnectionId, endpointId.ToString());
    }

    public Task UnsubscribeFromEndpoint(Guid endpointId)
    {
        return Groups.RemoveFromGroupAsync(Context.ConnectionId, endpointId.ToString());
    }
}
