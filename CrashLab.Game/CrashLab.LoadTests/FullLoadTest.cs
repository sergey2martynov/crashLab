using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.SignalR.Client;
using NBomber.Contracts;
using NBomber.CSharp;

namespace CrashLab.LoadTests;

// Фаза 7: полный нагрузочный прогон — держим пул WS-слушателей открытым на всю
// длительность теста, параллельно гоняем bet -> Running -> cashout по кругу на
// всех трёх столах несколько раундов подряд. Метрики latency broadcast'а и
// bet/cashout берёт сам NBomber-репорт + Report() ниже; lag Redis/Kafka смотреть
// в Grafana во время прогона (дашборды уже настроены с Фазы 1).
public static class FullLoadTest
{
    private const string GatewayUrl = "http://api.crashlab.local:8090";
    private static readonly TimeSpan TestDuration = TimeSpan.FromMinutes(5);

    // per-IP лимитер на Gateway — 20 permits/10с, но SignalR тратит 2 запроса на
    // соединение (negotiate + upgrade), см. Фазу 6. Группы по 8 держат это в
    // пределах лимита (16/20), так что почти весь пул реально подключается —
    // здесь мы не пере-тестируем сам лимитер, а хотим полноценный fan-out.
    private static async Task<ConcurrentBag<double>> StartTickListenersAsync(int count, int perIpGroupSize, IReadOnlyList<string> tokens)
    {
        var latencies = new ConcurrentBag<double>();

        var connectTasks = Enumerable.Range(0, count).Select(async i =>
        {
            var fakeIp = $"10.2.{i / perIpGroupSize}.1";
            var token = tokens[i % tokens.Count];
            var connection = new HubConnectionBuilder()
                .WithUrl($"{GatewayUrl}/gamehub", options =>
                {
                    options.Headers["X-Forwarded-For"] = fakeIp;
                    options.AccessTokenProvider = () => Task.FromResult(token)!;
                })
                .WithAutomaticReconnect()
                .Build();

            connection.On<TickDto>("ReceiveTick", tick =>
            {
                var latencyMs = (DateTimeOffset.UtcNow - tick.ServerTime).TotalMilliseconds;
                latencies.Add(latencyMs);
            });

            try
            {
                await connection.StartAsync();
                await connection.InvokeAsync("JoinTable", "table-1");
            }
            catch
            {
                // отклонённые лимитером — ожидаемо для части пула, не считаем это здесь
            }
        });

        await Task.WhenAll(connectTasks);
        return latencies;
    }

    private static ScenarioProps PlayerScenario(string name, HttpClient httpClient, string tableId, decimal betAmount, int copies, IReadOnlyList<string> tokens)
    {
        var tokenIndex = 0;
        string NextToken() => tokens[Interlocked.Increment(ref tokenIndex) % tokens.Count];

        return Scenario.Create(name, async context =>
            {
                var token = NextToken();
                // per-IP лимитер на Gateway (20 permits/10с) считает по X-Forwarded-For с
                // фоллбеком на RemoteIpAddress — без разброса весь трафик шёл бы с 127.0.0.1
                // и упирался в лимит почти сразу (см. Фазу 6). Хэш свежего Guid на каждую
                // итерацию даёт новый IP почти каждый раз — сам аккаунт при этом переиспользуется
                // из пула loadtest-токенов, это про диверсификацию IP, не про личность игрока.
                var fakeIp = $"10.3.{Math.Abs(Guid.NewGuid().GetHashCode()) % 250}.1";
                var connection = new HubConnectionBuilder()
                    .WithUrl($"{GatewayUrl}/gamehub", options =>
                    {
                        options.Headers["X-Forwarded-For"] = fakeIp;
                        options.AccessTokenProvider = () => Task.FromResult(token)!;
                    })
                    .Build();

                var waitingForBets = new TaskCompletionSource();
                var running = new TaskCompletionSource();
                var currentRoundId = Guid.Empty;

                connection.On<TickDto>("ReceiveTick", tick =>
                {
                    if (tick.TableId != tableId) return;

                    if (tick.State == "WaitingForBets" && !waitingForBets.Task.IsCompleted)
                    {
                        currentRoundId = tick.RoundId;
                        waitingForBets.TrySetResult();
                    }
                    else if (tick.State == "Running" && tick.RoundId == currentRoundId)
                    {
                        running.TrySetResult();
                    }
                });

                await connection.StartAsync();
                await connection.InvokeAsync("JoinTable", tableId);
                await waitingForBets.Task;

                var betRequest = new HttpRequestMessage(HttpMethod.Post, $"{GatewayUrl}/bets")
                {
                    Content = JsonContent.Create(new { Amount = betAmount, RoundId = currentRoundId, tableId })
                };
                betRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                betRequest.Headers.Add("X-Forwarded-For", fakeIp);
                var betResponse = await httpClient.SendAsync(betRequest);

                if (!betResponse.IsSuccessStatusCode)
                {
                    await connection.StopAsync();
                    return Response.Fail();
                }

                var bet = await betResponse.Content.ReadFromJsonAsync<BetDto>();

                await running.Task;

                var cashoutRequest = new HttpRequestMessage(HttpMethod.Post, $"{GatewayUrl}/bets/{bet!.Id}/cashout");
                cashoutRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                cashoutRequest.Headers.Add("X-Forwarded-For", fakeIp);
                var cashoutResponse = await httpClient.SendAsync(cashoutRequest);

                await connection.StopAsync();

                return cashoutResponse.IsSuccessStatusCode ? Response.Ok() : Response.Fail();
            })
            .WithWarmUpDuration(TimeSpan.FromSeconds(10))
            .WithLoadSimulations(
                Simulation.KeepConstant(copies: copies, during: TestDuration)
            );
    }

    public static async Task RunAsync(HttpClient httpClient)
    {
        var tokens = await AuthTokenPool.FetchAsync(httpClient);

        Console.WriteLine($"full load: connecting listener pool at {DateTimeOffset.UtcNow:O}");
        var latencies = await StartTickListenersAsync(count: 1000, perIpGroupSize: 8, tokens);
        Console.WriteLine($"full load: listeners connected, starting {TestDuration.TotalMinutes}-minute sustained run at {DateTimeOffset.UtcNow:O}");

        var scenarios = new[]
        {
            PlayerScenario("full_load_table1", httpClient, "table-1", 10m, copies: 40, tokens),
            PlayerScenario("full_load_table2", httpClient, "table-2", 50m, copies: 20, tokens),
            PlayerScenario("full_load_table3", httpClient, "table-3", 200m, copies: 10, tokens),
        };

        NBomberRunner.RegisterScenarios(scenarios).Run();

        Console.WriteLine($"full load: sustained run complete at {DateTimeOffset.UtcNow:O}");
        Console.WriteLine("full load: tick broadcast latency across the whole run:");
        WsListenersTest.Report(latencies);
    }
}
