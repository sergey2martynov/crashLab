using CrashLab.GameEngine.Grains;
using Microsoft.AspNetCore.Mvc;

namespace CrashLab.GameEngine.Controllers;

[ApiController]
[Route("[controller]")]
public class TablesController(
    IGrainFactory grainFactory) : ControllerBase
{
    
    [HttpGet("{baseTableId}/resolve")]
    public async Task<IResult> ResolveTable(string baseTableId)
    {
        var instanceId = await grainFactory.GetGrain<ITableManagerGrain>(0).ResolveTargetInstance(baseTableId);
        return Results.Ok(new { tableId = instanceId });
    }
}