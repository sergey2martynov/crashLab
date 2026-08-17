using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using CrashLab.GameEngine.Metrics;
using CrashLab.GameEngine.Repositories;
using Orleans.Placement;
using StackExchange.Redis;

namespace CrashLab.GameEngine.Grains;

[ActivationCountBasedPlacement]
public class RoundGrain(ILogger<RoundGrain> logger,
    ITableCatalog tableCatalog,
    [PersistentState("round", "roundStore")] IPersistentState<Round> state,
    IConnectionMultiplexer redis,
    IServiceScopeFactory serviceScopeFactory,
    GameMetrics gameMetrics)
    : Grain, IRoundGrain, IRemindable
{
    private const int BetWaitingDuration = 10;
    private const int PauseAfterCrashingDuration = 3;
    private TableConfig _config = null!;
    
    private static readonly TimeSpan SlowCallThreshold = TimeSpan.FromMilliseconds(50);
    
    private static readonly JsonSerializerOptions Options = new()
    {
        Converters = { new JsonStringEnumConverter() }
    };
    
    public override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        _config = tableCatalog.GetConfig(this.GetPrimaryKeyString());
        if (!state.RecordExists)
        {
            state.State = new Round(DateTimeOffset.UtcNow);
            await state.WriteStateAsync();
        }
        
        this.RegisterGrainTimer(TickAndPublish, TimeSpan.Zero, TimeSpan.FromMilliseconds(150));
        await this.RegisterOrUpdateReminder("heartbeat", TimeSpan.Zero, TimeSpan.FromMinutes(1));
        logger.LogInformation("Timer registered for {Table}", this.GetPrimaryKeyString());
        await base.OnActivateAsync(cancellationToken);
    }
    
    private async Task TickAndPublish()
    {
        logger.LogInformation("Tick fired for {Table}", this.GetPrimaryKeyString());
        var before = CurrentState();
        var multiplier = await GetMultiplier(before);

        var value = new
        {
            roundId = before.Id,
            tableId = this.GetPrimaryKeyString(),
            multiplier,
            state = before.State,
            serverTime = DateTimeOffset.UtcNow
        };
        await redis.GetSubscriber().PublishAsync(RedisChannel.Literal("round-ticks"),
            new RedisValue(JsonSerializer.Serialize(value, Options)));

        var wasRunning = before.State == RoundState.Running;
        gameMetrics.TickProcessed();
        var after = await Tick(DateTimeOffset.UtcNow);

        if (wasRunning && after.State == RoundState.Crashed)
        {
            using var scope = serviceScopeFactory.CreateScope();
            var outboxRepository = scope.ServiceProvider.GetRequiredService<IOutboxRepository>();
            await outboxRepository.AddEventAsync("round.crashed", new { roundId = after.Id, crashPoint = after.CrashPoint });
        }
    }

    private async Task<double> GetMultiplier(RoundDto s) =>
        s.State switch
        {
            RoundState.WaitingForBets => 1.0,
            RoundState.Crashed => s.CrashPoint,
            _ => await GetCurrentMultiplier(DateTimeOffset.UtcNow)
        };
    
    public async Task<RoundDto> Tick(DateTimeOffset now)
    {
        if (state.State.State == RoundState.WaitingForBets
            && now - state.State.StateEnteredAt > TimeSpan.FromSeconds(BetWaitingDuration))
        {
            state.State.State = RoundState.Running;
            state.State.StateEnteredAt = now;
            state.State.CrashPoint = GenerateCrashPoint();
            logger.LogInformation("Stage changed to {State}, CrashPoint: {CrashPoint}", state.State.State,
                state.State.CrashPoint);
            await state.WriteStateAsync();
        }
        if (state.State.State == RoundState.Running)
        {
            var multiplier = CalculateMultiplier(now);
            if (multiplier >= state.State.CrashPoint)
            {
                state.State.State = RoundState.Crashed;
                state.State.StateEnteredAt = now;
                logger.LogInformation("Stage changed to {State}, CrashPoint: {CrashPoint}", state.State.State,
                    state.State.CrashPoint);
                await state.WriteStateAsync();
            }
        }
        if (state.State.State == RoundState.Crashed
            && now - state.State.StateEnteredAt > TimeSpan.FromSeconds(PauseAfterCrashingDuration))
        {
            state.State = new Round(now);
            logger.LogInformation("Stage changed to {State}", state.State.State);
            await state.WriteStateAsync();
        }

        return CurrentState();
    }


    private double GenerateCrashPoint()
    {
        return 1 + Random.Shared.NextDouble() * 6;
    }
    
    public ValueTask<double> GetCurrentMultiplier(DateTimeOffset now)
    {
        return new ValueTask<double>(CalculateMultiplier(now));
    }
    
    private double CalculateMultiplier(DateTimeOffset now)
    {
        var elapsed = now - state.State.StateEnteredAt;
        return Math.Exp(0.1 * elapsed.TotalSeconds);
    }

    public Task<RoundDto> GetState()
    {
        return Task.FromResult(CurrentState());
    }

    private RoundDto CurrentState() => new(
        state.State.Id,
        state.State.State,
        state.State.CrashPoint,
        state.State.StateEnteredAt);

    public Task ReceiveReminder(string reminderName, TickStatus status) => Task.CompletedTask;
}