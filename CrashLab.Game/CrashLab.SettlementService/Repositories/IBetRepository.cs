using CrashLab.SettlementService.Bets;
using Npgsql;

namespace CrashLab.SettlementService.Repositories;

public interface IBetRepository
{
    Task AddAsync(Bet bet, NpgsqlTransaction transaction, CancellationToken ct);
    Task<Bet?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<Bet?> MarkCashedOutAsync(Guid id, decimal cashOut, NpgsqlTransaction transaction, CancellationToken ct);
    Task<IReadOnlyList<Bet>> SettleRoundAsync(Guid roundId, NpgsqlTransaction transaction, CancellationToken ct);
}