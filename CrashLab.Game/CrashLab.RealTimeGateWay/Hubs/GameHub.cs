using CrashLab.Game.Metrics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace CrashLab.RealTimeGateWay.Hubs;

[Authorize]
public class GameHub(GameMetrics gameMetrics, ILogger<GameHub> logger) : Hub
{
    public override Task OnConnectedAsync()
    {
        logger.LogInformation("SignalR connected, UserIdentifier={UserId}", Context.UserIdentifier);
        gameMetrics.ConnectionOpened();
        return base.OnConnectedAsync();
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        gameMetrics.ConnectionClosed();
        return base.OnDisconnectedAsync(exception);
    }
}
