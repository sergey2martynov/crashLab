namespace CrashLab.GameEngine.Dtos;

public class CompensationDto
{
    public Guid CompensationId { get; set; }
    public Guid AccountId { get; set; }
    public decimal Amount { get; set; }
}