namespace CrashLab.GameEngine.Grains;

[Alias("CrashLab.GameEngine.Grains.IRoundGrain")]
public interface IRoundGrain : IGrainWithStringKey
{
    Task<RoundDto> Tick(DateTimeOffset now);
    ValueTask<double> GetCurrentMultiplier(DateTimeOffset now);
    Task<RoundDto> GetState();
}