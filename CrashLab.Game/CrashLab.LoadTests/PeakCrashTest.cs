using System.Collections.Concurrent;
using System.Net.Http.Json;
using Microsoft.AspNetCore.SignalR.Client;

namespace CrashLab.LoadTests;

public static class PeakCrashTest
{
    public static async Task RunAsync(HttpClient httpClient)
    {
        var crashTimes = new ConcurrentDictionary<Guid, DateTimeOffset>();
        var crashListener = new HubConnectionBuilder()
            .WithUrl("http://localhost:5195/gamehub")
            .Build();

        crashListener.On<TickDto>("ReceiveTick", tick =>
        {
            if (tick.State == "Crashed")
                crashTimes.TryAdd(tick.RoundId, tick.ServerTime);
        });

        await crashListener.StartAsync();

        var waitingForBetsListener = new HubConnectionBuilder()
            .WithUrl("http://localhost:5195/gamehub")
            .Build();

        var waitingForBets = new TaskCompletionSource();
        var currentRoundId = Guid.Empty;
        waitingForBetsListener.On<TickDto>("ReceiveTick", tick =>
        {
            if (tick.State == "WaitingForBets" && tick.TableId == "table-1")
            {
                currentRoundId = tick.RoundId;
                waitingForBets.TrySetResult();
            }
        });

        await waitingForBetsListener.StartAsync();
        await waitingForBets.Task;
        Console.WriteLine($"peak crash: got round {currentRoundId} at {DateTimeOffset.UtcNow:O}");

        var placedBets = new ConcurrentBag<PlacedBet>();

        var placeBetTasks = Enumerable.Range(0, 1000).Select(async _ =>
        {
            var accountId = Guid.NewGuid();
            var request = new HttpRequestMessage(HttpMethod.Post, "http://localhost:5195/bets")
            {
                Content = JsonContent.Create(new { AccountId = accountId, Amount = 10m, RoundId = currentRoundId, tableId = "table-1" })
            };
            request.Headers.Add("X-Account-Id", accountId.ToString());
            var betResponse = await httpClient.SendAsync(request);
            
            if (betResponse.IsSuccessStatusCode)
            {
                var bet = await betResponse.Content.ReadFromJsonAsync<BetDto>();
                placedBets.Add(new PlacedBet(bet!.Id, accountId));
            }
        });

        await Task.WhenAll(placeBetTasks);
        Console.WriteLine($"peak crash: bets placed at {DateTimeOffset.UtcNow:O}");
        await waitingForBetsListener.StopAsync();

        var targetRoundId = Guid.Empty;
        var runningListener = new HubConnectionBuilder()
            .WithUrl("http://localhost:5195/gamehub")
            .Build();

        var running = new TaskCompletionSource();
        runningListener.On<TickDto>("ReceiveTick", tick =>
        {
            if (tick.State == "Running" && tick.TableId == "table-1")
            {
                targetRoundId = tick.RoundId;
                Console.WriteLine($"peak crash: caught Running tick, client={DateTimeOffset.UtcNow:O}, server={tick.ServerTime:O}, roundId={tick.RoundId}");
                if (tick.RoundId != currentRoundId)
                    Console.WriteLine($"peak crash: MISMATCH — already on next round! bets round={currentRoundId}, running round={tick.RoundId}");
                running.TrySetResult();
            }
        });

        await runningListener.StartAsync();
        await running.Task;
        await runningListener.StopAsync();

        var cashoutResults = new ConcurrentBag<CashoutRecord>();

        var cashoutTasks = placedBets.Select(async placedBet =>
        {
            var request = new HttpRequestMessage(HttpMethod.Post, $"http://localhost:5195/bets/{placedBet.BetId}/cashout");
            request.Headers.Add("X-Account-Id", placedBet.AccountId.ToString());
            var cashoutResponse = await httpClient.SendAsync(request);
            
            var timestamp = DateTimeOffset.UtcNow;
            cashoutResults.Add(new CashoutRecord(placedBet.BetId, cashoutResponse.IsSuccessStatusCode, cashoutResponse.StatusCode, timestamp));
        });

        await Task.WhenAll(cashoutTasks);
        
        var failuresByStatus = cashoutResults.Where(r => !r.Success).GroupBy(r => r.StatusCode);
        foreach (var g in failuresByStatus)
            Console.WriteLine($"peak crash: {g.Count()} failed with {g.Key}");

        while (!crashTimes.ContainsKey(targetRoundId))
        {
            await Task.Delay(200);
        }

        var crashInstant = crashTimes[targetRoundId];
        var tolerance = TimeSpan.FromMilliseconds(100);

        var successAfterCrash = cashoutResults.Count(r => r.Success && r.Timestamp > crashInstant + tolerance);
        var failBeforeCrash = cashoutResults.Count(r => !r.Success && r.Timestamp < crashInstant - tolerance);

        Console.WriteLine($"peak crash: round {targetRoundId}, crash recorded at {crashInstant:O}");
        Console.WriteLine($"peak crash: {cashoutResults.Count(r => r.Success)} succeeded / {cashoutResults.Count(r => !r.Success)} failed (of {cashoutResults.Count})");
        Console.WriteLine($"peak crash: {successAfterCrash} succeeded suspiciously after crash (>{tolerance.TotalMilliseconds}ms past)");
        Console.WriteLine($"peak crash: {failBeforeCrash} failed suspiciously before crash (>{tolerance.TotalMilliseconds}ms early)");

        var walletChecks = new ConcurrentBag<(Guid AccountId, Guid BetId, bool ShouldBeCredited, decimal ActualBalance)>();

        var balanceCheckTasks = placedBets.Select(async placedBet =>
        {
            var wallet = await httpClient.GetFromJsonAsync<WalletBalanceDto>(
                $"http://localhost:5013/balance/{placedBet.AccountId}");

            var cashout = cashoutResults.FirstOrDefault(r => r.BetId == placedBet.BetId);
            var shouldBeCredited = cashout?.Success ?? false;

            walletChecks.Add((placedBet.AccountId, placedBet.BetId, shouldBeCredited, wallet!.Balance));
        });

        await Task.WhenAll(balanceCheckTasks);

        var mismatches = walletChecks.Where(w => w.ShouldBeCredited != (w.ActualBalance != 990m)).ToList();

        Console.WriteLine($"peak crash: {mismatches.Count} balance mismatches (of {walletChecks.Count} accounts checked)");
        foreach (var m in mismatches)
        {
            Console.WriteLine($"  MISMATCH account={m.AccountId} bet={m.BetId} shouldBeCredited={m.ShouldBeCredited} balance={m.ActualBalance}");
        }

        await crashListener.StopAsync();
    }
}
