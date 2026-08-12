using Npgsql;

namespace CrashLab.GameEngine.Repositories;

public interface IOutboxRepository
{
    Task AddEventAsync(string eventType, object payload, NpgsqlTransaction? transaction = null);
    Task<IReadOnlyList<OutboxEvent>> GetUnpublishedEventsAsync(int batchSize);
    Task MarkPublishedAsync(Guid id);
}