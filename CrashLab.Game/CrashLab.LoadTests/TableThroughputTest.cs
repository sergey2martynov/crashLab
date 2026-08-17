using System.Diagnostics;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.SignalR.Client;

namespace CrashLab.LoadTests;

public static class TableThroughputTest
{
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
        var roundId = await tcs.Task;
        await listener.StopAsync();
        return roundId;
    }
    
    public static async Task RunAsync(HttpClient httpClient)
    {
        var tokens = await AuthTokenPool.FetchAsync(httpClient);
        var tokenIndex = 0;
        string NextToken() => tokens[Interlocked.Increment(ref tokenIndex) % tokens.Count];

        var table1RoundId = await WaitForRoundId("table-1", NextToken());
        var swSingle = Stopwatch.StartNew();

        var singleTableTasks = Enumerable.Range(0, 999).Select(async _ =>
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "http://api.crashlab.local:8090/bets")
            {
                Content = JsonContent.Create(new
                    { Amount = 50m, RoundId = table1RoundId, tableId = "table-1" })
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", NextToken());
            return await httpClient.SendAsync(request);
        });

        var singleResults = await Task.WhenAll(singleTableTasks);
        swSingle.Stop();
        var singleOk = singleResults.Count(r => r.IsSuccessStatusCode);

        var roundIds = await Task.WhenAll(
            WaitForRoundId("table-1", NextToken()),
            WaitForRoundId("table-2", NextToken()),
            WaitForRoundId("table-3", NextToken()));
        var (table1Next, table2Round, table3Round) = (roundIds[0], roundIds[1], roundIds[2]);

        var swMulti = Stopwatch.StartNew();

        Task<HttpResponseMessage> PlaceBet(Guid roundId, string tableId, decimal amount)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "http://api.crashlab.local:8090/bets")
            {
                Content = JsonContent.Create(new { Amount = amount, RoundId = roundId, tableId })
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", NextToken());
            return httpClient.SendAsync(request);
        }

        var multiTableTasks = new List<Task<HttpResponseMessage>>();
        multiTableTasks.AddRange(Enumerable.Range(0, 333).Select(_ => PlaceBet(table1Next, "table-1", 50m)));
        multiTableTasks.AddRange(Enumerable.Range(0, 333).Select(_ => PlaceBet(table2Round, "table-2", 50m)));
        multiTableTasks.AddRange(Enumerable.Range(0, 333).Select(_ => PlaceBet(table3Round, "table-3", 150m)));

        var multiResults = await Task.WhenAll(multiTableTasks);
        swMulti.Stop();
        var multiOk = multiResults.Count(r => r.IsSuccessStatusCode);

        
        Console.WriteLine($"throughput: single-table 999 bets ok={singleOk}/999, elapsed={swSingle.ElapsedMilliseconds}ms");
        Console.WriteLine($"throughput: multi-table 999 bets ok={multiOk}/999, elapsed={swMulti.ElapsedMilliseconds}ms");
    }
}