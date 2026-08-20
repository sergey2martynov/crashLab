namespace CrashLab.GameEngine.Grains;

public class TableManagerGrain(ITableCatalog tableCatalog, ILogger<TableManagerGrain> logger)
    : Grain, ITableManagerGrain
{
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(5);
    private const int OpenThreshold = 50;   // ставок за окно на самом нагруженном инстансе тира
    private const int CloseThreshold = 5;   // ставок за окно — ниже этого лишний инстанс закрывается

    private readonly Dictionary<string, List<DateTimeOffset>> _bets = new();
    private readonly HashSet<string> _openTables = new();
    private readonly Dictionary<string, int> _nextIndex = new(); // чтобы не переиспользовать id после закрытия

    public Task<bool> IsOpen(string tableId) => Task.FromResult(_openTables.Contains(tableId));
    
    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        foreach (var t in tableCatalog.GetAllTables())
            _openTables.Add(t.TableId);

        this.RegisterGrainTimer(EvaluateScaling, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
        return base.OnActivateAsync(cancellationToken);
    }

    public Task RecordBet(string tableId)
    {
        var now = DateTimeOffset.UtcNow;
        if (!_bets.TryGetValue(tableId, out var list))
        {
            list = new List<DateTimeOffset>();
            _bets[tableId] = list;
        }
        list.Add(now);
        list.RemoveAll(t => now - t > Window);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<string>> GetOpenTables() =>
        Task.FromResult<IReadOnlyList<string>>(_openTables.ToList());

    private async Task EvaluateScaling()
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var baseTable in tableCatalog.GetAllTables())
        {
            var instances = _openTables
                .Where(id => id == baseTable.TableId || id.StartsWith(baseTable.TableId + "-"))
                .ToList();

            var busiest = instances.Max(id => Volume(id, now));
            if (busiest > OpenThreshold)
            {
                var index = _nextIndex.GetValueOrDefault(baseTable.TableId, 1) + 1;
                _nextIndex[baseTable.TableId] = index;
                var newId = $"{baseTable.TableId}-{index}";
                _openTables.Add(newId);
                logger.LogInformation("Opening {Table} (busiest instance had {Count} bets)", newId, busiest);
                await GrainFactory.GetGrain<IRoundGrain>(newId).EnsureStarted();
            }

            foreach (var extra in instances.Where(id => id != baseTable.TableId))
            {
                if (Volume(extra, now) < CloseThreshold)
                {
                    _openTables.Remove(extra);
                    _bets.Remove(extra);
                    logger.LogInformation("Closing {Table} (idle)", extra);
                }
            }
        }
    }
    
    public Task<string> ResolveTargetInstance(string baseTableId)
    {
        var now = DateTimeOffset.UtcNow;
        var instances = _openTables
            .Where(id => id == baseTableId || id.StartsWith(baseTableId + "-"))
            .ToList();

        var target = instances
            .OrderBy(id => Volume(id, now))
            .First();

        return Task.FromResult(target);
    }

    private int Volume(string tableId, DateTimeOffset now) =>
        _bets.TryGetValue(tableId, out var list) ? list.Count(t => now - t <= Window) : 0;
}