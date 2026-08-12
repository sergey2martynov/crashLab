using System.Data;
using CrashLab.Game.Bets;
using Dapper;
using Npgsql;

namespace CrashLab.GameEngine.Repositories;

public class BetRepository(NpgsqlConnection connection) : IBetRepository
{
    public async Task AddAsync(Bet bet, NpgsqlTransaction transaction, CancellationToken ct)
    {
        const string sql = """
                           INSERT INTO bets (id, account_id, round_id, table_id, amount, status)
                           VALUES (@Id, @AccountId, @RoundId, @TableId, @Amount, @Status)
                           """;
        await connection.ExecuteAsync(new CommandDefinition(sql, new
        {
            bet.Id, bet.AccountId, bet.RoundId, bet.TableId, bet.Amount, Status = bet.Status.ToString()
        }, transaction, cancellationToken: ct));
    }

    public async Task<Bet?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        if (connection.State != ConnectionState.Open) await connection.OpenAsync(ct);

        const string sql = """
                           SELECT id AS Id, account_id AS AccountId, round_id AS RoundId,
                                  amount AS Amount, status AS Status, cash_out AS CashOut,
                                  table_id AS TableId
                           FROM bets WHERE id = @Id
                           """;
        return await connection.QuerySingleOrDefaultAsync<Bet>(new CommandDefinition(sql, new { Id = id }, cancellationToken: ct));
    }

    public async Task<Bet?> MarkCashedOutAsync(Guid id, decimal cashOut, NpgsqlTransaction transaction,CancellationToken ct)
    {
        if (connection.State != ConnectionState.Open) await connection.OpenAsync(ct);

        const string sql = """
                           UPDATE bets SET status = 'CashedOut', cash_out = @CashOut
                           WHERE id = @Id AND status = 'Active'
                           RETURNING id AS Id, account_id AS AccountId, round_id AS RoundId,
                                     amount AS Amount, status AS Status, cash_out AS CashOut,
                                     table_id AS TableId
                           """;
        return await connection.QuerySingleOrDefaultAsync<Bet>(new CommandDefinition(
            sql, new { Id = id, CashOut = cashOut },
            transaction,
            cancellationToken: ct));
    }
}