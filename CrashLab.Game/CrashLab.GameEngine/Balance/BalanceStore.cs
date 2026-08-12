using System.Collections.Concurrent;

namespace CrashLab.Game.Balance;

public class BalanceStore
{
    private readonly ConcurrentDictionary<Guid, decimal> _balances = new();

    public decimal GetBalance(Guid accountId) => _balances.GetOrAdd(accountId, 1000);

    public bool TryDebit(Guid accountId, decimal amount)
    {
        while (true)
        {
            var balance = _balances.GetOrAdd(accountId, 1000);
            if (balance < amount)
            {
                return false;
            }
            
            if(_balances.TryUpdate(accountId, balance - amount, balance)) return true;
        }
    }

    public void TryCredit(Guid accountId, decimal amount)
    {
        _balances.AddOrUpdate(accountId, amount, (_, b) => b + amount);
    }
}