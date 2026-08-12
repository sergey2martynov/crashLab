namespace CrashLab.GameEngine.Dtos;

public class BetRequest
{
    public Guid RoundId { get; set; }
    public decimal Amount { get; set; }
    public string TableId { get; set; }
}