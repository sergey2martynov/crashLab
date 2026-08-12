using CrashLab.Game.Metrics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace CrashLab.RealTimeGateWay.Hubs;

[Authorize]
public class GameHub(GameMetrics gameMetrics) : Hub
{
    public override Task OnConnectedAsync()
    {
        gameMetrics.ConnectionOpened();
        return base.OnConnectedAsync();
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        gameMetrics.ConnectionClosed();
        return base.OnDisconnectedAsync(exception);
    }
}
