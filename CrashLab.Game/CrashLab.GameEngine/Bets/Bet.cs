using CrashLab.GameEngine;
using CrashLab.GameEngine.Bets;

namespace CrashLab.Game.Bets;

public class Bet
{
    public Guid Id { get; set; }
    public Guid AccountId { get; set; }
    public Guid RoundId { get; set; }
    public decimal Amount { get; set; }
    public BetStatus Status { get; set; }
    public decimal CashOut { get; set; }
    public string TableId { get; set; }

    public static Bet CreateBet(Guid accountId, Guid roundId, decimal amount, string tableId)
    {
        return new Bet
        {
            Id = Guid.NewGuid(),
            AccountId = accountId,
            RoundId = roundId,
            Amount = amount,
            TableId = tableId,
            Status = BetStatus.Active
        };
    }
}