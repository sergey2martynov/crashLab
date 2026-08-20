using System.Text.Json;

namespace CrashLab.RealTimeGateWay.Clients;

public class GameEngineClient(HttpClient client) : IGameEngineClient
{
    public async Task<string> ResolveTableAsync(string baseTableId, CancellationToken ct)
    {
        var response = await client.GetAsync($"tables/{baseTableId}/resolve", ct);
        response.EnsureSuccessStatusCode();
        var doc = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
        return doc.GetProperty("tableId").GetString()!;
    }
}