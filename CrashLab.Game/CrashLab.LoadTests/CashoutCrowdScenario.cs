using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.SignalR.Client;
using NBomber.Contracts;
using NBomber.CSharp;

namespace CrashLab.LoadTests;

public static class CashoutCrowdScenario
{
    public static ScenarioProps Create(HttpClient httpClient, IReadOnlyList<string> tokens)
    {
        var tokenIndex = 0;
        string NextToken() => tokens[Interlocked.Increment(ref tokenIndex) % tokens.Count];

        return Scenario.Create("cashout_crowd", async context =>
            {
                var token = NextToken();
                var connection = new HubConnectionBuilder()
                    .WithUrl("http://api.crashlab.local:8090/gamehub", options =>
                    {
                        options.AccessTokenProvider = () => Task.FromResult(token)!;
                    })
                    .Build();

                // ждём WaitingForBets
                var waitingForBets = new TaskCompletionSource();
                var currentRoundId = Guid.Empty;
                connection.On<TickDto>("ReceiveTick", tick =>
                {
                    if (tick.State == "WaitingForBets" && tick.TableId == "table-1")
                    {
                        currentRoundId = tick.RoundId;
                        waitingForBets.TrySetResult();
                    }
                });

                await connection.StartAsync();
                await connection.InvokeAsync("JoinTable", "table-1");
                await waitingForBets.Task;

                // ставка
                var request = new HttpRequestMessage(HttpMethod.Post, "http://api.crashlab.local:8090/bets")
                {
                    Content = JsonContent.Create(new { Amount = 10m, RoundId = currentRoundId, tableId = "table-1" })
                };
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                var betResponse = await httpClient.SendAsync(request);

                if (!betResponse.IsSuccessStatusCode) return Response.Fail();

                var bet = await betResponse.Content.ReadFromJsonAsync<BetDto>();
                var betId = bet!.Id;

                // ждём Running
                var running = new TaskCompletionSource();
                connection.On<TickDto>("ReceiveTick", tick =>
                {
                    if (tick.State == "Running"  && tick.TableId == "table-1")
                        running.TrySetResult();
                });
                await running.Task;

                // cashout
                var cashoutRequest = new HttpRequestMessage(HttpMethod.Post, $"http://api.crashlab.local:8090/bets/{betId}/cashout");
                cashoutRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                var cashoutResponse = await httpClient.SendAsync(cashoutRequest);

                await connection.StopAsync();

                return cashoutResponse.IsSuccessStatusCode ? Response.Ok() : Response.Fail();
            })
            .WithWarmUpDuration(TimeSpan.FromSeconds(5))
            .WithLoadSimulations(
                Simulation.KeepConstant(copies: 20, during: TimeSpan.FromSeconds(60))
            );
    }
}
