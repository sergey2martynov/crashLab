namespace CrashLab.GameEngine.Grains;

public class TableCatalog : ITableCatalog
{
    private static readonly TableConfig[] Tables =
    [
        new("table-1", 1m, 100m),
        new("table-2", 10m, 1000m),
        new("table-3", 100m, 10000m),
    ];

    public TableConfig GetConfig(string tableId)
    {
        var exact = Tables.FirstOrDefault(t => t.TableId == tableId);
        if (exact is not null) return exact;

        var dashIndex = tableId.LastIndexOf('-');
        if (dashIndex > 0)
        {
            var baseConfig = Tables.FirstOrDefault(t => t.TableId == tableId[..dashIndex]);
            if (baseConfig is not null) return baseConfig;
        }

        throw new InvalidOperationException($"Unknown table {tableId}");
    }

    public IReadOnlyList<TableConfig> GetAllTables() => Tables;
}