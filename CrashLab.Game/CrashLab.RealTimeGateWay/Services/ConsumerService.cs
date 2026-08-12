using System.Diagnostics;
using System.Text.Json;
using Confluent.Kafka;
using CrashLab.RealTimeGateWay.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace CrashLab.RealTimeGateWay.Services;

public class ConsumerService(
    ILogger<ConsumerService> logger,
    IProducer<string, string> producer,
    IHubContext<GameHub> hubContext) : BackgroundService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private IConsumer<string, string>? _consumer;

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var config = new ConsumerConfig
        {
            BootstrapServers = "localhost:9092",
            GroupId = $"realtime-gateway-{Guid.NewGuid()}",
            EnableAutoCommit = false,
            AutoOffsetReset = AutoOffsetReset.Latest
        };

        _consumer = new ConsumerBuilder<string, string>(config).Build();
        _consumer.Subscribe(["bet.settled"]);

        return Task.Factory.StartNew(
            () => ConsumeLoop(stoppingToken),
            stoppingToken,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default);
    }

    private void ConsumeLoop(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            ConsumeResult<string, string>? result = null;
            try
            {
                result = _consumer!.Consume(stoppingToken);
                HandleMessage(result, stoppingToken).GetAwaiter().GetResult();
                _consumer.Commit(result);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                if (result is not null)
                {
                    var envelope = JsonSerializer.Serialize(new
                    {
                        originalTopic = result.Topic,
                        key = result.Message.Key,
                        value = result.Message.Value,
                        error = ex.Message,
                        failedAt = DateTimeOffset.UtcNow
                    });

                    producer.ProduceAsync("dead-letter", new Message<string, string>
                    {
                        Key = result.Message.Key,
                        Value = envelope
                    }).GetAwaiter().GetResult();
                    
                    _consumer!.Commit(result);
                }
                
                logger.LogWarning(ex, "Failed to process message from {Topic}, sent to dead-letter queue", result?.Topic);
            }
        }
    }

    private async Task HandleMessage(ConsumeResult<string, string> result, CancellationToken ct)
    {
        var time = Stopwatch.StartNew();

        switch (result.Topic)
        {
            case "bet.settled":
            {
                var evt = JsonSerializer.Deserialize<BetSettledEvent>(result.Message.Value, JsonOptions)!;
                
                await hubContext.Clients.User(evt.AccountId.ToString()).SendAsync("BetSettled", new
                {
                    evt.BetId,
                    evt.RoundId,
                    evt.Status,
                    evt.Amount
                });
                break;
            }
        }
        
        time.Stop();
    }

    public override void Dispose()
    {
        _consumer?.Close();
        _consumer?.Dispose();
        base.Dispose();
    }
    
    private record BetSettledEvent(Guid BetId, Guid RoundId, Guid AccountId, string Status, decimal Amount);
}