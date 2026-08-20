using CrashLab.Game.Metrics;
using CrashLab.RealTimeGateWay.Clients;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace CrashLab.RealTimeGateWay.Hubs;

[Authorize]
public class GameHub(GameMetrics gameMetrics,
    ILogger<GameHub> logger,
    IGameEngineClient gameEngineClient) : Hub
{
    public override Task OnConnectedAsync()
    {
        logger.LogInformation("SignalR connected, UserIdentifier={UserId}", Context.UserIdentifier);
        gameMetrics.ConnectionOpened();
        return base.OnConnectedAsync();
    }
    
    public async Task<string> JoinTable(string baseTableId)
    {
        var instanceId = await gameEngineClient.ResolveTableAsync(baseTableId, Context.ConnectionAborted);
        await Groups.AddToGroupAsync(Context.ConnectionId, instanceId);
        return instanceId;
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        gameMetrics.ConnectionClosed();
        return base.OnDisconnectedAsync(exception);
    }
}
