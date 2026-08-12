using System.Diagnostics;
using CrashLab.WalletService.Exceptions;
using CrashLab.WalletService.Models;
using CrashLab.WalletService.Repositories;

namespace CrashLab.WalletService.Services;

public class WalletService(IWalletRepository repository,
    ILogger<WalletService> logger) : IWalletService
{
    public async Task<Wallet> GetBalanceAsync(Guid accountId, CancellationToken ct)
    {
        await repository.EnsureWalletExistsAsync(accountId, ct);
        return await repository.GetWalletAsync(accountId, ct) ?? throw new NotFoundException("Wallet does not exist");
    }

    public async Task<Wallet> CreditAsync(Guid accountId, decimal amount, CancellationToken ct)
    {
        await repository.EnsureWalletExistsAsync(accountId, ct);
        return await repository.UpdateBalanceAsync(accountId, amount, ct);
    }
    
    public async Task<Wallet> DebitAsync(Guid accountId, DebitDto dto, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        await repository.EnsureWalletExistsAsync(accountId, ct);
        logger.LogInformation("EnsureWalletExistsAsync: {Ms}ms", sw.ElapsedMilliseconds);
        sw.Restart();
        var wallet = await repository.DebitBalanceAsync(accountId, dto.Amount, ct);
        logger.LogInformation("DebitBalanceAsync: {Ms}ms", sw.ElapsedMilliseconds);
        if (wallet is null)
            throw new InsufficientFundsException("Not enough balance");

        return wallet;
    }

    public async Task<Wallet> CashOutAsync(Guid accountId, CashOutDto dto, CancellationToken ct)
    {
        var affectedRows = await repository.EnsureCashOutAsync(accountId, dto, ct);
        if (affectedRows == 0)
            return await repository.GetWalletAsync(accountId, ct) ?? throw new NotFoundException("Wallet does not exist");

        return await CreditAsync(accountId, dto.Amount, ct);
    }

    public async Task<Wallet> CompensateCreditAsync(Guid accountId, CompensationDto dto, CancellationToken ct)
    {
        var rows = await repository.EnsureCompensationCreditAsync(accountId, dto, ct);
        var wallet = await repository.GetWalletAsync(accountId, ct) ?? throw new NotFoundException("Wallet does not exist");
        if (rows == 0) return wallet;
        return await CreditAsync(accountId, dto.Amount, ct);
    }
    
    public async Task<Wallet> CompensateDebitAsync(Guid accountId, CompensationDto dto, CancellationToken ct)
    {
        var rows = await repository.EnsureCompensationDebitAsync(accountId, dto, ct);
        var wallet = await repository.GetWalletAsync(accountId, ct) ?? throw new NotFoundException("Wallet does not exist");
        if (rows == 0) return wallet;
        return await repository.CompensateDebitBalanceAsync(accountId, dto.Amount, ct);
    }
}