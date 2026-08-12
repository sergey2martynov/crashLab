using CrashLab.WalletService.Models;

namespace CrashLab.WalletService.Services;

public interface IWalletService
{
    Task<Wallet> GetBalanceAsync(Guid accountId, CancellationToken ct);

    Task<Wallet> CreditAsync(Guid accountId, decimal amount, CancellationToken ct);

    Task<Wallet> CashOutAsync(Guid accountId, CashOutDto dto, CancellationToken ct);

    Task<Wallet>DebitAsync(Guid accountId, DebitDto dto, CancellationToken ct);
    Task<Wallet> CompensateCreditAsync(Guid accountId, CompensationDto dto, CancellationToken ct);
    Task<Wallet> CompensateDebitAsync(Guid accountId, CompensationDto dto, CancellationToken ct);
}
