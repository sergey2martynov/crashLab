namespace CrashLab.GameEngine.Grains;

public interface ITableManagerGrain : IGrainWithIntegerKey
{
    Task RecordBet(string tableId);
    Task<IReadOnlyList<string>> GetOpenTables();
    Task<bool> IsOpen(string tableId);
    Task<string> ResolveTargetInstance(string baseTableId);
}