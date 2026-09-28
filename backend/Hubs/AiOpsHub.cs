using Microsoft.AspNetCore.SignalR;

namespace backend.Hubs;

public sealed class AiOpsHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        await base.OnConnectedAsync();
    }
}