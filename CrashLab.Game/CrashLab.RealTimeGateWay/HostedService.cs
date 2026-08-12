using System.Text.Json;
using CrashLab.RealTimeGateWay.Hubs;
using Microsoft.AspNetCore.SignalR;
using StackExchange.Redis;

namespace CrashLab.RealTimeGateWay;

public class HostedService(IConnectionMultiplexer redis, IHubContext<GameHub> hubContext) : IHostedService
{
    public async  Task StartAsync(CancellationToken cancellationToken)
    {
        var subscriber = redis.GetSubscriber();
        var queue = await subscriber.SubscribeAsync(RedisChannel.Literal("round-ticks"));
        queue.OnMessage(async channelMessage =>
        {
            var tick = JsonSerializer.Deserialize<JsonElement>(channelMessage.Message.ToString());
            await hubContext.Clients.All.SendAsync("ReceiveTick", tick);
        });
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}