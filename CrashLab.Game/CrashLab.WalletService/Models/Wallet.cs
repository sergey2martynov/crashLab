namespace CrashLab.WalletService.Models;

public class Wallet
{
    public Guid AccountId { get; set; }
    public decimal Balance { get; set; }
    public int Version { get; set; }
}