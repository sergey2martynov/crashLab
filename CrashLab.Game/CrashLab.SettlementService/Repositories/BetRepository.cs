using System.Data;
using CrashLab.SettlementService.Bets;
using Dapper;
using Npgsql;

namespace CrashLab.SettlementService.Repositories;

public class BetRepository(NpgsqlConnection connection) : IBetRepository
{
    public async Task AddAsync(Bet bet, NpgsqlTransaction transaction, CancellationToken ct)
    {
        const string sql = """
                           INSERT INTO bets (id, account_id, round_id, amount, status)
                           VALUES (@Id, @AccountId, @RoundId, @Amount, @Status)
                           ON CONFLICT (id) DO NOTHING
                           """;
        await connection.ExecuteAsync(new CommandDefinition(sql, new
        {
            bet.Id, bet.AccountId, bet.RoundId, bet.Amount, Status = bet.Status.ToString()
        }, transaction, cancellationToken: ct));
    }

    public async Task<Bet?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        if (connection.State != ConnectionState.Open) await connection.OpenAsync(ct);

        const string sql = """
                           SELECT id AS Id, account_id AS AccountId, round_id AS RoundId,
                                  amount AS Amount, status AS Status, cash_out AS CashOut
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
                                     amount AS Amount, status AS Status, cash_out AS CashOut
                           """;
        return await connection.QuerySingleOrDefaultAsync<Bet>(new CommandDefinition(
            sql, new { Id = id, CashOut = cashOut },
            transaction,
            cancellationToken: ct));
    }
    
    public async Task<IReadOnlyList<Bet>> SettleRoundAsync(Guid roundId, NpgsqlTransaction transaction, CancellationToken ct)
    {
        const string lostSql = """
                               UPDATE bets SET status = 'Lost', settled_at = now()
                               WHERE round_id = @RoundId AND status = 'Active'
                               RETURNING id AS Id, account_id AS AccountId, round_id AS RoundId,
                                         amount AS Amount, status AS Status, cash_out AS CashOut
                               """;

        const string cashedOutSql = """
                                    UPDATE bets SET settled_at = now()
                                    WHERE round_id = @RoundId AND status = 'CashedOut' AND settled_at IS NULL
                                    RETURNING id AS Id, account_id AS AccountId, round_id AS RoundId,
                                              amount AS Amount, status AS Status, cash_out AS CashOut
                                    """;

        var lost = await connection.QueryAsync<Bet>(new CommandDefinition(
            lostSql, new { RoundId = roundId }, transaction, cancellationToken: ct));

        var cashedOut = await connection.QueryAsync<Bet>(new CommandDefinition(
            cashedOutSql, new { RoundId = roundId }, transaction, cancellationToken: ct));

        return lost.Concat(cashedOut).ToList();
    }
}