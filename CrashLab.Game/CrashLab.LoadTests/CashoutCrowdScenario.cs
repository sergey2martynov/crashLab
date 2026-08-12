using System.Net.Http.Json;
using Microsoft.AspNetCore.SignalR.Client;
using NBomber.Contracts;
using NBomber.CSharp;

namespace CrashLab.LoadTests;

public static class CashoutCrowdScenario
{
    public static ScenarioProps Create(HttpClient httpClient)
    {
        return Scenario.Create("cashout_crowd", async context =>
            {
                var connection = new HubConnectionBuilder()
                    .WithUrl("http://localhost:5195/gamehub")
                    .Build();

                // ждём WaitingForBets
                var waitingForBets = new TaskCompletionSource();
                var currentRoundId = Guid.Empty;
                connection.On<TickDto>("ReceiveTick", tick =>
                {
                    if (tick.ToString()!.Contains("WaitingForBets") && tick.TableId == "table-1")
                    {
                        currentRoundId = tick.RoundId;
                        waitingForBets.TrySetResult();
                    }
                });

                await connection.StartAsync();
                await waitingForBets.Task;

                // ставка
                var userId = Guid.NewGuid();
                
                var request = new HttpRequestMessage(HttpMethod.Post, "http://localhost:5195/bets")
                {
                    Content = JsonContent.Create(new { AccountId = userId, Amount = 10m, RoundId = currentRoundId, tableId = "table-1" })
                };
                request.Headers.Add("X-Account-Id", userId.ToString());
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
                var cashoutRequest = new HttpRequestMessage(HttpMethod.Post, $"http://localhost:5195/bets/{betId}/cashout");
                cashoutRequest.Headers.Add("X-Account-Id", userId.ToString());
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
