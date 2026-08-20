using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.SignalR.Client;

namespace CrashLab.LoadTests;

public static class TableIsolationTest
{
    public static async Task RunAsync(HttpClient httpClient)
    {
        var tokens = await AuthTokenPool.FetchAsync(httpClient);
        var currentRoundId = Guid.Empty;
        var waitingForBets = new TaskCompletionSource();

        var listener = new HubConnectionBuilder()
            .WithUrl("http://api.crashlab.local:8090/gamehub", options =>
            {
                options.AccessTokenProvider = () => Task.FromResult(tokens[0])!;
            })
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
        await listener.InvokeAsync("JoinTable", "table-2");
        await waitingForBets.Task;

        var request = new HttpRequestMessage(HttpMethod.Post, "http://api.crashlab.local:8090/bets")
        {
            Content = JsonContent.Create(new { Amount = 5m, RoundId = currentRoundId, tableId = "table-2" })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokens[1]);
        var belowLimitResponse = await httpClient.SendAsync(request);

        request = new HttpRequestMessage(HttpMethod.Post, "http://api.crashlab.local:8090/bets")
        {
            Content = JsonContent.Create(new { Amount = 50m, RoundId = currentRoundId, tableId = "table-2" })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokens[2]);
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
