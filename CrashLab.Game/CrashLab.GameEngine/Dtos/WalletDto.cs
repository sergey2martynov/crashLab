namespace CrashLab.GameEngine.Dtos;

public class WalletDto
{
    public Guid AccountId { get; set; }
    public decimal Balance { get; set; }
    public int Version { get; set; }
}