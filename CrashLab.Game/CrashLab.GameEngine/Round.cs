namespace CrashLab.GameEngine;

public class Round(DateTimeOffset stateEnteredAt)
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public double CrashPoint { get; set; }
    public RoundState State { get; set; } = RoundState.WaitingForBets;  
    public DateTimeOffset StateEnteredAt { get; set; } = stateEnteredAt;
}