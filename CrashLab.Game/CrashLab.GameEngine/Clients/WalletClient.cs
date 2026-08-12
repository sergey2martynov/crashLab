using CrashLab.GameEngine.Dtos;

namespace CrashLab.GameEngine.Clients;

public class WalletClient(HttpClient client, ILogger<WalletClient> logger) : IWalletClient
{
    public async Task<WalletDto> DebitAsync(DebitDto dto, CancellationToken ct)
    {
        logger.LogInformation("walletClient: sending debit account={AccountId} at {Time:O}", dto.AccountId, DateTimeOffset.UtcNow);
        var response = await client.PostAsJsonAsync("balance/debit", dto, ct);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<WalletDto>(ct))!;
    }

    public async Task<WalletDto> CashOutAsync(CashOutDto dto, CancellationToken ct)
    {
        var response = await client.PostAsJsonAsync("balance/cashout", dto, ct);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<WalletDto>(ct))!;
    }

    public async Task<WalletDto> CompensateCreditAsync(CompensationDto dto, CancellationToken ct)
    {
        var response = await client.PostAsJsonAsync("balance/compensate-credit", dto, ct);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<WalletDto>(ct))!;
    }

    public async Task<WalletDto> CompensateDebitAsync(CompensationDto dto, CancellationToken ct)
    {
        var response = await client.PostAsJsonAsync("balance/compensate-debit", dto, ct);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<WalletDto>(ct))!;
    }
}