using System.Diagnostics.Metrics;

namespace CrashLab.SettlementService.Metrics;

public class SettlementMetrics
{
    public const string MeterName = "CrashLab.Game.Settlement";
    
    private readonly Counter<int> _eventsConsumed;
    private readonly Counter<int>  _eventsFailed;
    private readonly Histogram<double> _settlementDuration;
    private readonly Counter<int>  _betsSettled;

    public SettlementMetrics()
    {
        var meter = new Meter(MeterName);
        _eventsConsumed = meter.CreateCounter<int>(
            "settlement.events.consumed",
            description: "Number of handled messages");
        _eventsFailed = meter.CreateCounter<int>(
            "settlement.events.failed",
            description: "Number of failed messages");
        _settlementDuration = meter.CreateHistogram<double>(
            "settlement.event.duration",
            unit: "ms",
            description: "Duration of settlement proccess");
        _betsSettled = meter.CreateCounter<int>(
            "settlement.bets.settled",
            description: "Number of bets settled");
    }

    public void EventConsumed(string topic) =>  _eventsConsumed.Add(1, new KeyValuePair<string, object?>("topic", topic));
    public void EventsFailed(string topic) => _eventsFailed.Add(1, new KeyValuePair<string, object?>("topic", topic));
    public void RecordSettlementDuration(string topic, double milliseconds) => _settlementDuration.Record(milliseconds, new KeyValuePair<string, object?>("topic", topic));
    public void BetSettled(string status) => _betsSettled.Add(1, new KeyValuePair<string, object?>("status", status));
}