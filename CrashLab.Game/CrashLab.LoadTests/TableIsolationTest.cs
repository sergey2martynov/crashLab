using System.Collections.Concurrent;
using System.Net.Http.Json;
using Microsoft.AspNetCore.SignalR.Client;

namespace CrashLab.LoadTests;

public static class TableIsolationTest
{
    public static async Task RunAsync(HttpClient httpClient)
    {
        var currentRoundId = Guid.Empty;
        var waitingForBets = new TaskCompletionSource();

        var listener = new HubConnectionBuilder()
            .WithUrl("http://localhost:5195/gamehub")
            .Build();

        listener.On<TickDto>("ReceiveTick", tick =>
        {
            if (tick.TableId == "table-2" && tick.State == "WaitingForBets")
            {
                currentRoundId = tick.RoundId;
                waitingForBets.TrySetResult();
            }
        });

        await listener.StartAsync();
        await waitingForBets.Task;
        
        var belowLimitAccountId = Guid.NewGuid();
        var request = new HttpRequestMessage(HttpMethod.Post, "http://localhost:5195/bets")
        {
            Content = JsonContent.Create(new { AccountId = belowLimitAccountId, Amount = 5m, RoundId = currentRoundId, tableId = "table-2" })
        };
        request.Headers.Add("X-Account-Id", belowLimitAccountId.ToString());
        var belowLimitResponse = await httpClient.SendAsync(request);

        var validAccountId = Guid.NewGuid();
        request = new HttpRequestMessage(HttpMethod.Post, "http://localhost:5195/bets")
        {
            Content = JsonContent.Create(new { AccountId = validAccountId, Amount = 50m, RoundId = currentRoundId, tableId = "table-2" })
        };
        request.Headers.Add("X-Account-Id", validAccountId.ToString());
        var validResponse = await httpClient.SendAsync(request);
        
        await listener.StopAsync();
        
        Console.WriteLine(belowLimitResponse.IsSuccessStatusCode
            ? "table isolation: FAIL — ставка ниже MinBet была принята"
            : "table isolation: OK — ставка ниже MinBet отклонена");

        Console.WriteLine(validResponse.IsSuccessStatusCode
            ? "table isolation: OK — ставка в пределах лимита принята"
            : "table isolation: FAIL — валидная ставка отклонена");
    }
}