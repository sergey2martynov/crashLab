using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using CrashLab.GameEngine.Grains;
using CrashLab.GameEngine.Metrics;
using CrashLab.GameEngine.Repositories;
using StackExchange.Redis;

namespace CrashLab.GameEngine;

public class GameLoopHostedService(IGrainFactory grainFactory,
    IConnectionMultiplexer redis,
    GameMetrics gameMetrics,
    IServiceScopeFactory serviceScopeFactory,
    ITableCatalog tableCatalog) : BackgroundService
{
    private static readonly JsonSerializerOptions Options = new()
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private static readonly TimeSpan SlowCallThreshold = TimeSpan.FromMilliseconds(50);

    private static async Task<T> TimedGrainCall<T>(string label, string tableId, Task<T> call)
    {
        var sw = Stopwatch.StartNew();
        var result = await call;
        sw.Stop();
        if (sw.Elapsed > SlowCallThreshold)
        {
            Console.WriteLine($"game loop: SLOW {label} table={tableId} took {sw.ElapsedMilliseconds}ms");
        }
        return result;
    }

    private static async Task TimedGrainCall(string label, string tableId, Task call)
    {
        var sw = Stopwatch.StartNew();
        await call;
        sw.Stop();
        if (sw.Elapsed > SlowCallThreshold)
        {
            Console.WriteLine($"game loop: SLOW {label} table={tableId} took {sw.ElapsedMilliseconds}ms");
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var tables = tableCatalog.GetAllTables();

            foreach (var table in tables)
            {
                var grain = grainFactory.GetGrain<IRoundGrain>(table.TableId);
                var state = await TimedGrainCall("GetState", table.TableId, grain.GetState());
                gameMetrics.TickProcessed();
            
                var subscriber = redis.GetSubscriber();
                var value = new
                {
                    roundId = state.Id,
                    tableId = table.TableId,
                    multiplier = await GetMultiplier(state, grain),
                    state = state.State,
                    serverTime = DateTimeOffset.UtcNow
                };
            
                await subscriber.PublishAsync(RedisChannel.Literal("round-ticks"), new RedisValue(JsonSerializer.Serialize(value, Options)));
            
                var wasRunning = state.State == RoundState.Running;

                state = await TimedGrainCall("Tick", table.TableId, grain.Tick(DateTimeOffset.UtcNow));

                if (wasRunning && state.State == RoundState.Crashed)
                {
                    using var scope = serviceScopeFactory.CreateScope();
                    var outboxRepository = scope.ServiceProvider.GetRequiredService<IOutboxRepository>();
                    await outboxRepository.AddEventAsync("round.crashed", new
                    {
                        roundId = state.Id,
                        crashPoint = state.CrashPoint
                    });
                }
            }
            
            await Task.Delay(TimeSpan.FromMilliseconds(150), stoppingToken);
        }
    }

    private async Task<double> GetMultiplier(RoundDto state, IRoundGrain grain)
    {
        if (state.State == RoundState.WaitingForBets)
        {
            return 1.0;
        }
        
        if (state.State == RoundState.Crashed)
        {
            return state.CrashPoint;
        }

        return await grain.GetCurrentMultiplier(DateTimeOffset.UtcNow);
    }
}