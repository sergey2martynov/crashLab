using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR.Client;

namespace CrashLab.LoadTests;

public record ChaosEvent(DateTimeOffset Time, int ConnectionIndex, string Type, string? Detail);

public static class WsListenersTest
{
    public static async Task<(ConcurrentBag<double> Latencies, ConcurrentBag<ChaosEvent> Events)> StartAsync()
    {
        var latencies = new ConcurrentBag<double>();
        var events = new ConcurrentBag<ChaosEvent>();

        for (var i = 0; i < 500; i++)
        {
            var fakeIp = $"10.0.{i / 25}.1";
            var index = i;
            var connection = new HubConnectionBuilder()
                .WithUrl("http://localhost:5195/gamehub", options =>
                {
                    options.Headers["X-Forwarded-For"] = fakeIp;
                })
                .WithAutomaticReconnect()
                .Build();

            connection.On<TickDto>("ReceiveTick", tick =>
            {
                var latencyMs = (DateTimeOffset.UtcNow - tick.ServerTime).TotalMilliseconds;
                latencies.Add(latencyMs);
            });

            connection.Reconnecting += error =>
            {
                events.Add(new ChaosEvent(DateTimeOffset.UtcNow, index, "reconnecting", error?.Message));
                return Task.CompletedTask;
            };

            connection.Reconnected += connectionId =>
            {
                events.Add(new ChaosEvent(DateTimeOffset.UtcNow, index, "reconnected", connectionId));
                return Task.CompletedTask;
            };

            connection.Closed += error =>
            {
                events.Add(new ChaosEvent(DateTimeOffset.UtcNow, index, "closed", error?.Message));
                return Task.CompletedTask;
            };

            try
            {
                await connection.StartAsync();
            }
            catch (Exception ex)
            {
                events.Add(new ChaosEvent(DateTimeOffset.UtcNow, index, "rejected", ex.Message));
            }
        }

        return (latencies, events);
    }

    public static void Report(ConcurrentBag<double> latencies)
    {
        var sorted = latencies.OrderBy(x => x).ToArray();

        Console.WriteLine($"count = {sorted.Length}");
        Console.WriteLine($"min   = {sorted.First():F2} ms");
        Console.WriteLine($"mean  = {sorted.Average():F2} ms");
        Console.WriteLine($"p50   = {Percentile(sorted, 50):F2} ms");
        Console.WriteLine($"p95   = {Percentile(sorted, 95):F2} ms");
        Console.WriteLine($"p99   = {Percentile(sorted, 99):F2} ms");
    }

    private static double Percentile(double[] data, double p)
    {
        if (data.Length == 0) return 0;
        var index = (int)Math.Ceiling(p / 100.0 * data.Length) - 1;
        index = Math.Clamp(index, 0, data.Length - 1);
        return data[index];
    }

    public static void ReportChaos(ConcurrentBag<ChaosEvent> events)
    {
        var sorted = events.OrderBy(e => e.Time).ToList();
        Console.WriteLine($"reconnecting: {sorted.Count(e => e.Type == "reconnecting")}");
        Console.WriteLine($"reconnected:  {sorted.Count(e => e.Type == "reconnected")}");
        Console.WriteLine($"closed:       {sorted.Count(e => e.Type == "closed")}");
        Console.WriteLine($"rejected: {sorted.Count(e => e.Type == "rejected")}");
        Directory.CreateDirectory("reports");
        var lines = sorted.Select(e => $"[{e.Time:HH:mm:ss.fff}] #{e.ConnectionIndex} {e.Type} {e.Detail}");
        File.WriteAllLines($"reports/chaos-{DateTime.Now:yyyyMMdd-HHmmss}.log", lines);
    }
}
