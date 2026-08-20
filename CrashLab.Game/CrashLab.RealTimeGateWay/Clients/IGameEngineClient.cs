namespace CrashLab.RealTimeGateWay.Clients;

public interface IGameEngineClient
{
    Task<string> ResolveTableAsync(string baseTableId, CancellationToken ct);
}