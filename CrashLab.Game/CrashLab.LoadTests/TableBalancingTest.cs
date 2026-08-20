using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.SignalR.Client;

namespace CrashLab.LoadTests;

public static class TableBalancingTest
{
    public static async Task RunAsync(HttpClient httpClient)
    {
        var tokens = await AuthTokenPool.FetchAsync(httpClient);
        var tokenIndex = 0;
        string NextToken() => tokens[Interlocked.Increment(ref tokenIndex) % tokens.Count];

        // 1. разгоняем table-1, чтобы TableManagerGrain открыл table-1-2
        var table1RoundId = await WaitForRoundId("table-1", NextToken());
        var burst = Enumerable.Range(0, 999).Select(async _ =>
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "http://api.crashlab.local:8090/bets")
            {
                Content = JsonContent.Create(new { Amount = 50m, RoundId = table1RoundId, tableId = "table-1" })
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", NextToken());
            return await httpClient.SendAsync(request);
        });
        await Task.WhenAll(burst);

        Console.WriteLine("balancing: waiting for EvaluateScaling tick (30s)...");
        await Task.Delay(TimeSpan.FromSeconds(35));

        // 2. N независимых сессий заходят на table-1 (как фронт) и реально ставят
        // на тот инстанс, который им резолвнули — не только резолв, но и RecordBet
        var sessions = await Task.WhenAll(Enumerable.Range(0, 20).Select(async _ =>
        {
            var token = NextToken();
            var connection = new HubConnectionBuilder()
                .WithUrl("http://api.crashlab.local:8090/gamehub", options =>
                {
                    options.AccessTokenProvider = () => Task.FromResult(token)!;
                })
                .Build();

            var waitingForBets = new TaskCompletionSource<Guid>();
            connection.On<TickDto>("ReceiveTick", tick =>
            {
                if (tick.State == "WaitingForBets")
                    waitingForBets.TrySetResult(tick.RoundId);
            });

            await connection.StartAsync();
            var instanceId = await connection.InvokeAsync<string>("JoinTable", "table-1");
            var roundId = await waitingForBets.Task;

            var request = new HttpRequestMessage(HttpMethod.Post, "http://api.crashlab.local:8090/bets")
            {
                Content = JsonContent.Create(new { Amount = 50m, RoundId = roundId, tableId = instanceId })
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var response = await httpClient.SendAsync(request);

            await connection.StopAsync();
            return (instanceId, ok: response.IsSuccessStatusCode);
        }));

        Console.WriteLine("balancing: bets per resolved instance:");
        foreach (var group in sessions.GroupBy(s => s.instanceId))
            Console.WriteLine($"  {group.Key}: {group.Count()} sessions, {group.Count(s => s.ok)} bets ok");
    }

    private static async Task<Guid> WaitForRoundId(string tableId, string token)
    {
        var tcs = new TaskCompletionSource<Guid>();
        var listener = new HubConnectionBuilder()
            .WithUrl("http://api.crashlab.local:8090/gamehub", options =>
            {
                options.AccessTokenProvider = () => Task.FromResult(token)!;
            })
            .Build();

        listener.On<TickDto>("ReceiveTick", tick =>
        {
            if (tick.TableId == tableId && tick.State == "WaitingForBets")
                tcs.TrySetResult(tick.RoundId);
        });

        await listener.StartAsync();
        await listener.InvokeAsync("JoinTable", tableId);
        var roundId = await tcs.Task;
        await listener.StopAsync();
        return roundId;
    }
}
