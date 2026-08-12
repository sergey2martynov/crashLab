using System.Diagnostics.Metrics;

namespace CrashLab.GameEngine.Metrics;

public class GameMetrics
{
    public const string MeterName = "CrashLab.Game";

    private readonly UpDownCounter<int> _activeConnections;
    private readonly Counter<int> _ticksTotal;
    private readonly Histogram<double> _betDuration;

    public GameMetrics()
    {
        var meter = new Meter(MeterName);
        _activeConnections = meter.CreateUpDownCounter<int>(
            "game.connections.active",
            description: "Number of active WebSocket connections");
        _ticksTotal = meter.CreateCounter<int>(
            "game.ticks.total",
            description: "Total number of game loop ticks processed");
        _betDuration = meter.CreateHistogram<double>(
            "game.bet.duration",
            unit: "ms",
            description: "Duration of bet/cashout endpoint processing");
    }

    public void ConnectionOpened() => _activeConnections.Add(1);
    public void ConnectionClosed() => _activeConnections.Add(-1);
    public void TickProcessed() => _ticksTotal.Add(1);
    public void RecordBetDuration(double milliseconds) => _betDuration.Record(milliseconds);
}