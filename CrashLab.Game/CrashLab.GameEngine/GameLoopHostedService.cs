using CrashLab.GameEngine.Grains;

namespace CrashLab.GameEngine;

public class GameLoopHostedService(IGrainFactory grainFactory,
    ITableCatalog tableCatalog) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        foreach (var table in tableCatalog.GetAllTables())
        {
            await grainFactory.GetGrain<IRoundGrain>(table.TableId).EnsureStarted();
        }
    }
}