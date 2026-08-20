using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.SignalR.Client;

namespace CrashLab.LoadTests;

// Волна = один раунд: WaveSize юзеров заходят одновременно, ставят в один и тот
// же раунд, кэшаутятся одновременно у одного и того же краша (пиковая гонка
// сохраняется внутри волны), по сумме волн дотягиваем до WaveCount*WaveSize
// уникальных участников без пересечения аккаунтов между волнами.
//
// Важно: JoinTable("table-1") может резолвиться в другой инстанс (table-1-N),
// если сам table-1 уже нагружен предыдущими волнами — используем ИМЕННО
// резолвленный id везде внутри волны (фильтр тиков, ставка), не литерал
// "table-1", иначе тики целевого инстанса никогда не пройдут фильтр.
public static class PeakCrashTest
{
    private const int WaveCount = 10;
    private const int WaveSize = 100;
    private const string GatewayUrl = "http://api.crashlab.local:8090";

    public static async Task RunAsync(HttpClient httpClient)
    {
        var tokens = await AuthTokenPool.FetchAsync(httpClient, WaveCount * WaveSize);

        var allCashoutResults = new ConcurrentBag<CashoutRecord>();
        var allWalletChecks = new ConcurrentBag<(Guid BetId, bool ShouldBeCredited, decimal Baseline, decimal Actual)>();

        for (var wave = 0; wave < WaveCount; wave++)
        {
            var waveTokens = tokens.Skip(wave * WaveSize).Take(WaveSize).ToList();
            Console.WriteLine($"peak crash: wave {wave + 1}/{WaveCount} starting at {DateTimeOffset.UtcNow:O}");

            var baselines = new ConcurrentDictionary<string, decimal>();
            await Task.WhenAll(waveTokens.Select(async token =>
                baselines[token] = await GetBalance(httpClient, token)));

            var anchor = new HubConnectionBuilder()
                .WithUrl($"{GatewayUrl}/gamehub", options =>
                {
                    options.AccessTokenProvider = () => Task.FromResult(waveTokens[0])!;
                })
                .Build();

            var waitingForBets = new TaskCompletionSource<Guid>();
            var running = new TaskCompletionSource<bool>();
            var crashed = new TaskCompletionSource<DateTimeOffset>();
            string? instanceId = null;

            anchor.On<TickDto>("ReceiveTick", tick =>
            {
                if (instanceId is null || tick.TableId != instanceId) return;
                switch (tick.State)
                {
                    case "WaitingForBets":
                        waitingForBets.TrySetResult(tick.RoundId);
                        break;
                    case "Running":
                        running.TrySetResult();
                        break;
                    case "Crashed":
                        crashed.TrySetResult(tick.ServerTime);
                        break;
                }
            });

            await anchor.StartAsync();
            instanceId = await anchor.InvokeAsync<string>("JoinTable", "table-1");
            Console.WriteLine($"peak crash: wave {wave + 1}/{WaveCount} resolved to {instanceId}");

            var currentRoundId = await WaitWithTimeout(waitingForBets.Task, TimeSpan.FromSeconds(35), $"wave {wave + 1} WaitingForBets on {instanceId}");
            if (currentRoundId is null)
            {
                await anchor.StopAsync();
                continue;
            }

            var placedBets = new ConcurrentBag<(Guid BetId, string Token)>();

            var placeBetTasks = waveTokens.Select(async token =>
            {
                var request = new HttpRequestMessage(HttpMethod.Post, $"{GatewayUrl}/bets")
                {
                    Content = JsonContent.Create(new { Amount = 10m, RoundId = currentRoundId.Value, tableId = instanceId })
                };
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                var betResponse = await httpClient.SendAsync(request);

                if (betResponse.IsSuccessStatusCode)
                {
                    var bet = await betResponse.Content.ReadFromJsonAsync<BetDto>();
                    placedBets.Add((bet!.Id, token));
                }
            });

            await Task.WhenAll(placeBetTasks);

            var reachedRunning = await WaitWithTimeout(running.Task, TimeSpan.FromSeconds(15), $"wave {wave + 1} Running on {instanceId}");

            var waveCashoutResults = new ConcurrentBag<CashoutRecord>();

            if (reachedRunning is not null)
            {
                var cashoutTasks = placedBets.Select(async placedBet =>
                {
                    var request = new HttpRequestMessage(HttpMethod.Post, $"{GatewayUrl}/bets/{placedBet.BetId}/cashout");
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", placedBet.Token);
                    var cashoutResponse = await httpClient.SendAsync(request);

                    var record = new CashoutRecord(placedBet.BetId, cashoutResponse.IsSuccessStatusCode, cashoutResponse.StatusCode, DateTimeOffset.UtcNow);
                    waveCashoutResults.Add(record);
                    allCashoutResults.Add(record);
                });

                await Task.WhenAll(cashoutTasks);
            }

            var crashedAt = await WaitWithTimeout(crashed.Task, TimeSpan.FromSeconds(60), $"wave {wave + 1} Crashed on {instanceId}");
            await anchor.StopAsync();

            var balanceCheckTasks = placedBets.Select(async placedBet =>
            {
                var actual = await GetBalance(httpClient, placedBet.Token);
                var shouldBeCredited = waveCashoutResults.FirstOrDefault(r => r.BetId == placedBet.BetId)?.Success ?? false;
                allWalletChecks.Add((placedBet.BetId, shouldBeCredited, baselines[placedBet.Token], actual));
            });

            await Task.WhenAll(balanceCheckTasks);

            Console.WriteLine($"peak crash: wave {wave + 1}/{WaveCount} done — instance={instanceId}, {placedBets.Count} bets placed, {waveCashoutResults.Count(r => r.Success)} cashed out, crashed at {(crashedAt.HasValue ? crashedAt.Value.ToString("O") : "unknown")}");
        }

        var failuresByStatus = allCashoutResults.Where(r => !r.Success).GroupBy(r => r.StatusCode);
        foreach (var g in failuresByStatus)
            Console.WriteLine($"peak crash: {g.Count()} failed with {g.Key}");

        Console.WriteLine($"peak crash: {allCashoutResults.Count(r => r.Success)} succeeded / {allCashoutResults.Count(r => !r.Success)} failed (of {allCashoutResults.Count})");

        // не кэшаутнутый — баланс ровно baseline-10 (только дебет); кэшаутнутый — должен
        // отличаться от baseline-10 (кредит поверх дебета)
        var mismatches = allWalletChecks
            .Where(w => w.ShouldBeCredited != (w.Actual != w.Baseline - 10m))
            .ToList();

        Console.WriteLine($"peak crash: {mismatches.Count} balance mismatches (of {allWalletChecks.Count} accounts checked)");
        foreach (var m in mismatches)
        {
            Console.WriteLine($"  MISMATCH bet={m.BetId} shouldBeCredited={m.ShouldBeCredited} baseline={m.Baseline} actual={m.Actual}");
        }
    }

    private static async Task<T?> WaitWithTimeout<T>(Task<T> task, TimeSpan timeout, string what) where T : struct
    {
        var completed = await Task.WhenAny(task, Task.Delay(timeout));
        if (completed != task)
        {
            Console.WriteLine($"peak crash: WARNING — timed out waiting for {what} after {timeout.TotalSeconds}s");
            return null;
        }
        return await task;
    }

    private static async Task<decimal> GetBalance(HttpClient httpClient, string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"{GatewayUrl}/balance");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await httpClient.SendAsync(request);
        var wallet = await response.Content.ReadFromJsonAsync<WalletBalanceDto>();
        return wallet!.Balance;
    }
}
