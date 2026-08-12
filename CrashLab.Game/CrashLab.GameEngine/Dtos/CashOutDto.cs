namespace CrashLab.GameEngine.Dtos;

public class CashOutDto
{
    public Guid CashOutId { get; set; }
    public Guid AccountId { get; set; }
    public decimal Amount { get; set; }
}