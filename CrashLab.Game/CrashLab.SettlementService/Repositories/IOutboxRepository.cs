using Npgsql;

namespace CrashLab.SettlementService.Repositories;

public interface IOutboxRepository
{
    Task AddEventAsync(string eventType, object payload, NpgsqlTransaction? transaction = null);
    Task<IReadOnlyList<OutboxEvent>> GetUnpublishedEventsAsync(int batchSize);
    Task MarkPublishedAsync(Guid id);
}