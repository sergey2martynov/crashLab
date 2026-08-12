namespace CrashLab.SettlementService.Bets;

public class Bet
{
    public Guid Id { get; set; }
    public Guid AccountId { get; set; }
    public Guid RoundId { get; set; }
    public decimal Amount { get; set; }
    public BetStatus Status { get; set; }
    public decimal CashOut { get; set; }
}