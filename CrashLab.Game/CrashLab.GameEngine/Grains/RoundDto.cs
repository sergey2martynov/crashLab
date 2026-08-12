namespace CrashLab.GameEngine.Grains;

[GenerateSerializer]
public record RoundDto(
    [property: Id(0)] Guid Id,
    [property: Id(1)] RoundState State,
    [property: Id(2)] double CrashPoint,
    [property: Id(3)] DateTimeOffset StateEnteredAt);