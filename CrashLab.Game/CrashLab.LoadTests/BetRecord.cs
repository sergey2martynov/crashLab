using System.Net;
using System.Text.Json.Serialization;

namespace CrashLab.LoadTests;

record BetDto([property: JsonPropertyName("id")] Guid Id);

record PlacedBet(Guid BetId, Guid AccountId);

record CashoutRecord(Guid BetId, bool Success, HttpStatusCode StatusCode, DateTimeOffset Timestamp);

record WalletBalanceDto(
    [property: JsonPropertyName("accountId")] Guid AccountId,
    [property: JsonPropertyName("balance")] decimal Balance,
    [property: JsonPropertyName("version")] int Version);