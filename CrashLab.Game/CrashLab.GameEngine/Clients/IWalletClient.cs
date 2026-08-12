using CrashLab.GameEngine.Dtos;

namespace CrashLab.GameEngine.Clients;

public interface IWalletClient
{
    Task<WalletDto> DebitAsync(DebitDto dto, CancellationToken ct);
    Task<WalletDto> CashOutAsync(CashOutDto dto, CancellationToken ct);
    Task<WalletDto> CompensateCreditAsync(CompensationDto dto, CancellationToken ct);
    Task<WalletDto> CompensateDebitAsync(CompensationDto dto, CancellationToken ct);
}