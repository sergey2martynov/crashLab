using CrashLab.WalletService.Models;
using Dapper;
using Npgsql;

namespace CrashLab.WalletService.Repositories;

public class WalletRepository : IWalletRepository
{
    private const string SelectColumns = "account_Id, balance, version";

    private readonly NpgsqlConnection _connection;

    public WalletRepository(NpgsqlConnection connection)
    {
        _connection = connection;
    }

    public async Task EnsureWalletExistsAsync(Guid accountId, CancellationToken ct)
    {
        if (_connection.State != System.Data.ConnectionState.Open)
            await _connection.OpenAsync(ct);

        await _connection.ExecuteAsync(
            """
            INSERT INTO wallets (account_id, balance, version)
            VALUES (@account_id, @balance, @version)
            ON CONFLICT (account_id) DO NOTHING
            """,
            new { account_id = accountId, balance = Constants.DefaultBalance, version = 0 });
    }

    public async Task<Wallet?> GetWalletAsync(Guid accountId, CancellationToken ct)
    {
        if (_connection.State != System.Data.ConnectionState.Open)
            await _connection.OpenAsync(ct);
        
        return await _connection.QuerySingleOrDefaultAsync<Wallet>(
            $"""
             SELECT {SelectColumns} FROM wallets w
             WHERE account_Id = @account_id
             """,
            new { account_Id = accountId });
    }

    public async Task<Wallet> UpdateBalanceAsync(Guid accountId, decimal amount, CancellationToken ct)
    {
        if (_connection.State != System.Data.ConnectionState.Open)
            await _connection.OpenAsync(ct);
        
        return await _connection.QuerySingleOrDefaultAsync<Wallet>(
            """
            UPDATE wallets
            SET balance = balance + @amount, version = version + 1
            WHERE account_Id = @account_id
            RETURNING account_id, balance, version
            """,
            new { account_id = accountId, amount });
    }
    
    public async Task<Wallet?> DebitBalanceAsync(Guid accountId, decimal amount, CancellationToken ct)
    {
        if (_connection.State != System.Data.ConnectionState.Open)
            await _connection.OpenAsync(ct);

        return await _connection.QuerySingleOrDefaultAsync<Wallet>(
            """
            UPDATE wallets
            SET balance = balance - @amount, version = version + 1
            WHERE account_id = @account_id AND balance >= @amount
            RETURNING account_id, balance, version
            """,
            new { account_id = accountId, amount })!;
    }
    
    public async Task<int> EnsureCashOutAsync(Guid accountId, CashOutDto dto, CancellationToken ct)
    {
        if (_connection.State != System.Data.ConnectionState.Open)
            await _connection.OpenAsync(ct);
        
        var rowAffected = await _connection.ExecuteAsync(
            """
            INSERT INTO processed_cashout (id, amount, account_id)
            VALUES (@id, @amount, @account_id)
            ON CONFLICT (id) DO NOTHING
            """,
            new { id = dto.CashOutId, amount = dto.Amount, account_id = accountId });
        
        return rowAffected;
    }

    public async Task<int> EnsureCompensationCreditAsync(Guid accountId, CompensationDto dto,CancellationToken ct)
    {
        if (_connection.State != System.Data.ConnectionState.Open)
            await _connection.OpenAsync(ct);
        
        var rowAffected = await _connection.ExecuteAsync(
            """
            INSERT INTO processed_compensation (id, amount, account_id, direction)
            VALUES (@id, @amount, @account_id, @direction)
            ON CONFLICT (id) DO NOTHING
            """,
            new { id = dto.CompensationId, amount = dto.Amount, account_id = accountId, direction = "credit" });
        
        return rowAffected;
    }
    
    public async Task<int> EnsureCompensationDebitAsync(Guid accountId, CompensationDto dto, CancellationToken ct)
    {
        if (_connection.State != System.Data.ConnectionState.Open)
            await _connection.OpenAsync(ct);
        
        var rowAffected = await _connection.ExecuteAsync(
            """
            INSERT INTO processed_compensation (id, amount, account_id, direction)
            VALUES (@id, @amount, @account_id, @direction)
            ON CONFLICT (id) DO NOTHING
            """,
            new { id = dto.CompensationId, amount = dto.Amount, account_id = accountId, direction = "debit" });
        
        return rowAffected;
    }

    public async Task<Wallet> CompensateDebitBalanceAsync(Guid accountId, decimal amount, CancellationToken ct)
    {
        if (_connection.State != System.Data.ConnectionState.Open)
            await _connection.OpenAsync(ct);

        return await _connection.QuerySingleOrDefaultAsync<Wallet>(
            """
            UPDATE wallets
            SET balance = balance - @amount, version = version + 1
            WHERE account_id = @account_id
            RETURNING account_id, balance, version
            """,
            new { account_id = accountId, amount })!;
    }
}
