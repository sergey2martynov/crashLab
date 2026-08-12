using System.Data;
using System.Text.Json;
using Dapper;
using Npgsql;

namespace CrashLab.GameEngine.Repositories;

public class OutboxRepository(NpgsqlConnection connection) : IOutboxRepository
{
    public async Task AddEventAsync(string eventType, object payload, NpgsqlTransaction? transaction = null)
    {
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync();
        }

        const string sql = "INSERT INTO outbox_events (event_type, payload) VALUES (@EventType, @Payload::jsonb)";
        await connection.ExecuteAsync(sql, new
        {
            EventType = eventType,
            Payload = JsonSerializer.Serialize(payload)
        },
        transaction);
    }
    
    public async Task<IReadOnlyList<OutboxEvent>> GetUnpublishedEventsAsync(int batchSize)
    {
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync();
        }

        const string sql = """
                           SELECT id AS Id, event_type AS EventType, payload::text AS Payload
                           FROM outbox_events
                           WHERE published_at IS NULL
                           ORDER BY created_at
                           LIMIT @BatchSize
                           """;
        var rows = await connection.QueryAsync<OutboxEvent>(sql, new { BatchSize = batchSize });
        return rows.ToList();
    }

    public async Task MarkPublishedAsync(Guid id)
    {
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync();
        }

        const string sql = "UPDATE outbox_events SET published_at = now() WHERE id = @Id";
        await connection.ExecuteAsync(sql, new { Id = id });
    }
}

public record OutboxEvent(Guid Id, string EventType, string Payload);