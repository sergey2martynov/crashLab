using CrashLab.WalletService.Models;

namespace CrashLab.WalletService.Repositories;

public interface IWalletRepository
{
    Task EnsureWalletExistsAsync(Guid accountId, CancellationToken ct);

    Task<Wallet?> GetWalletAsync(Guid accountId, CancellationToken ct);

    Task<Wallet> UpdateBalanceAsync(Guid accountId, decimal amount, CancellationToken ct);

    Task<int> EnsureCashOutAsync(Guid accountId, CashOutDto dto, CancellationToken ct);
    Task<Wallet?> DebitBalanceAsync(Guid accountId, decimal amount, CancellationToken ct);
    Task<int> EnsureCompensationCreditAsync(Guid accountId, CompensationDto dto, CancellationToken ct);
    Task<int> EnsureCompensationDebitAsync(Guid accountId, CompensationDto dto, CancellationToken ct);
    Task<Wallet> CompensateDebitBalanceAsync(Guid accountId, decimal amount, CancellationToken ct);
}
