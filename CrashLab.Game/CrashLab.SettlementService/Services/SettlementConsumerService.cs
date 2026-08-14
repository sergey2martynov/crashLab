using System.Diagnostics;
using System.Text.Json;
using Confluent.Kafka;
using CrashLab.SettlementService.Bets;
using CrashLab.SettlementService.Metrics;
using CrashLab.SettlementService.Repositories;
using Npgsql;

namespace CrashLab.SettlementService.Services;

public class SettlementConsumerService(
    IServiceScopeFactory scopeFactory,
    ILogger<SettlementConsumerService> logger,
    SettlementMetrics metrics,
    IProducer<string, string> producer,
    IConfiguration configuration) : BackgroundService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private IConsumer<string, string>? _consumer;

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var config = new ConsumerConfig
        {
            BootstrapServers = configuration["Kafka:BootstrapServers"],
            GroupId = "settlement-service",
            EnableAutoCommit = false,
            AutoOffsetReset = AutoOffsetReset.Earliest
        };

        _consumer = new ConsumerBuilder<string, string>(config).Build();
        _consumer.Subscribe(["bet.placed", "bet.cashed_out", "round.crashed"]);

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
                    metrics.EventsFailed(result.Topic);
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
        using var scope = scopeFactory.CreateScope();
        var betRepository = scope.ServiceProvider.GetRequiredService<IBetRepository>();
        var outboxRepository = scope.ServiceProvider.GetRequiredService<IOutboxRepository>();
        var connection = scope.ServiceProvider.GetRequiredService<NpgsqlConnection>();

        if (connection.State != System.Data.ConnectionState.Open)
            await connection.OpenAsync(ct);

        switch (result.Topic)
        {
            case "bet.placed":
            {
                var evt = JsonSerializer.Deserialize<BetPlacedEvent>(result.Message.Value, JsonOptions)!;
                await using var transaction = await connection.BeginTransactionAsync(ct);
                await betRepository.AddAsync(new Bet
                {
                    Id = evt.BetId,
                    AccountId = evt.AccountId,
                    RoundId = evt.RoundId,
                    Amount = evt.Amount,
                    Status = BetStatus.Active
                }, transaction, ct);
                metrics.EventConsumed("bet.placed");
                await transaction.CommitAsync(ct);
                break;
            }
            case "bet.cashed_out":
            {
                var evt = JsonSerializer.Deserialize<BetCashedOutEvent>(result.Message.Value, JsonOptions)!;
                await using var transaction = await connection.BeginTransactionAsync(ct);
                await betRepository.MarkCashedOutAsync(evt.BetId, evt.Amount, transaction, ct);
                metrics.EventConsumed("bet.cashed_out");
                await transaction.CommitAsync(ct);
                break;
            }
            case "round.crashed":
            {
                var evt = JsonSerializer.Deserialize<RoundCrashedEvent>(result.Message.Value, JsonOptions)!;
                await using var transaction = await connection.BeginTransactionAsync(ct);
                var settled = await betRepository.SettleRoundAsync(evt.RoundId, transaction, ct);
                foreach (var bet in settled)
                {
                    await outboxRepository.AddEventAsync("bet.settled", new
                    {
                        betId = bet.Id,
                        roundId = bet.RoundId,
                        accountId = bet.AccountId,
                        status = bet.Status.ToString(),
                        amount = bet.Status == BetStatus.Lost ? bet.Amount : bet.CashOut
                    }, transaction);
                    
                    metrics.BetSettled(bet.Status.ToString());
                }
                metrics.EventConsumed("round.crashed");
                
                await transaction.CommitAsync(ct);
                break;
            }
        }
        
        time.Stop();
        metrics.RecordSettlementDuration(result.Topic, time.Elapsed.TotalMilliseconds);
    }

    private record BetPlacedEvent(Guid BetId, Guid RoundId, Guid AccountId, decimal Amount);
    private record BetCashedOutEvent(Guid BetId, Guid RoundId, Guid AccountId, decimal Amount);
    private record RoundCrashedEvent(Guid RoundId, double CrashPoint);

    public override void Dispose()
    {
        _consumer?.Close();
        _consumer?.Dispose();
        base.Dispose();
    }
}