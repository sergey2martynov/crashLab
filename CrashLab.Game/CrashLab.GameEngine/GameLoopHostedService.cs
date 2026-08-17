using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using CrashLab.GameEngine.Grains;
using CrashLab.GameEngine.Metrics;
using CrashLab.GameEngine.Repositories;
using StackExchange.Redis;

namespace CrashLab.GameEngine;

public class GameLoopHostedService(IGrainFactory grainFactory,
    ITableCatalog tableCatalog) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        foreach (var table in tableCatalog.GetAllTables())
        {
            await grainFactory.GetGrain<IRoundGrain>(table.TableId).GetState();
        }
    }
}