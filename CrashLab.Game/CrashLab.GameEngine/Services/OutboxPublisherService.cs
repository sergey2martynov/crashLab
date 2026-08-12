using System.Text.Json;
using Confluent.Kafka;
using CrashLab.GameEngine.Repositories;

namespace CrashLab.GameEngine.Services;

public class OutboxPublisherService(
    IServiceScopeFactory scopeFactory,
    IProducer<string, string> producer,
    ILogger<OutboxPublisherService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var repository = scope.ServiceProvider.GetRequiredService<IOutboxRepository>();

                var events = await repository.GetUnpublishedEventsAsync(batchSize: 100);

                foreach (var outboxEvent in events)
                {
                    try
                    {
                        var key = ExtractRoundId(outboxEvent.Payload);
                        await producer.ProduceAsync(outboxEvent.EventType, new Message<string, string>
                        {
                            Key = key,
                            Value = outboxEvent.Payload
                        }, stoppingToken);

                        await repository.MarkPublishedAsync(outboxEvent.Id);
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex, "Failed to publish outbox event {EventId}, will retry next poll", outboxEvent.Id);
                    }
                }

                await Task.Delay(TimeSpan.FromMilliseconds(500), stoppingToken);
            }
            catch (Exception e)
            {
                await Task.Delay(500, stoppingToken);
                logger.LogWarning(e, "Outbox poll iteration failed, will retry after backoff");
            }
            
        }
    }

    private static string ExtractRoundId(string payload)
    {
        using var doc = JsonDocument.Parse(payload);
        return doc.RootElement.GetProperty("roundId").GetString()!;
    }
}