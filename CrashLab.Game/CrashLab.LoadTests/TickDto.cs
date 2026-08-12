using System.Text.Json.Serialization;

public record TickDto
{
    [JsonPropertyName("roundId")]
    public Guid RoundId { get; init; }

    [JsonPropertyName("multiplier")]
    public double Multiplier { get; init; }

    [JsonPropertyName("state")]
    public string State { get; init; } = "";

    [JsonPropertyName("serverTime")]
    public DateTimeOffset ServerTime { get; init; }
    [JsonPropertyName("tableId")]
    public string TableId { get; init; } = "";
}