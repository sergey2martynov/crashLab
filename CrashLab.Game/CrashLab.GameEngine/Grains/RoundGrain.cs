namespace CrashLab.GameEngine.Grains;

public class RoundGrain(ILogger<RoundGrain> logger,
    ITableCatalog tableCatalog) : Grain, IRoundGrain
{
    private const int BetWaitingDuration = 10;
    private const int PauseAfterCrashingDuration = 3;
    private TableConfig _config = null!;
    private Round _currentRound = new(DateTimeOffset.UtcNow);
    
    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        _config = tableCatalog.GetConfig(this.GetPrimaryKeyString());
        return base.OnActivateAsync(cancellationToken);
    }
    
    public Task<RoundDto> Tick(DateTimeOffset now)
    {
        if (_currentRound.State == RoundState.WaitingForBets
            && now - _currentRound.StateEnteredAt > TimeSpan.FromSeconds(BetWaitingDuration))
        {
            _currentRound.State = RoundState.Running;
            _currentRound.StateEnteredAt = now;
            _currentRound.CrashPoint = GenerateCrashPoint();
            logger.LogInformation("Stage changed to {State}, CrashPoint: {CrashPoint}", _currentRound.State,
                _currentRound.CrashPoint);
        }
        if (_currentRound.State == RoundState.Running)
        {
            var multiplier = CalculateMultiplier(now);
            if (multiplier >= _currentRound.CrashPoint)
            {
                _currentRound.State = RoundState.Crashed;
                _currentRound.StateEnteredAt = now;
                logger.LogInformation("Stage changed to {State}, CrashPoint: {CrashPoint}", _currentRound.State,
                    _currentRound.CrashPoint);
            }
        }
        if (_currentRound.State == RoundState.Crashed
            && now - _currentRound.StateEnteredAt > TimeSpan.FromSeconds(PauseAfterCrashingDuration))
        {
            _currentRound = new Round(now);
            logger.LogInformation("Stage changed to {State}", _currentRound.State);
        }

        return Task.FromResult(CurrentState());
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
        var elapsed = now - _currentRound.StateEnteredAt;
        return Math.Exp(0.1 * elapsed.TotalSeconds);
    }

    public Task<RoundDto> GetState()
    {
        return Task.FromResult(CurrentState());
    }

    private RoundDto CurrentState() => new(
        _currentRound.Id,
        _currentRound.State,
        _currentRound.CrashPoint,
        _currentRound.StateEnteredAt);
}