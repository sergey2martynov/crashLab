namespace CrashLab.GameEngine;

[GenerateSerializer]
public class Round(DateTimeOffset stateEnteredAt)
{
    [Id(0)] public Guid Id { get; set; } = Guid.NewGuid();
    [Id(1)] public double CrashPoint { get; set; }
    [Id(2)] public RoundState State { get; set; } = RoundState.WaitingForBets;
    [Id(3)] public DateTimeOffset StateEnteredAt { get; set; } = stateEnteredAt;
}