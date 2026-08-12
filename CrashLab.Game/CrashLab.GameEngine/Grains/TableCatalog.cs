namespace CrashLab.GameEngine.Grains;

public class TableCatalog : ITableCatalog
{
    private static readonly TableConfig[] Tables =
    [
        new("table-1", 1m, 100m),
        new("table-2", 10m, 1000m),
        new("table-3", 100m, 10000m),
    ];

    public TableConfig GetConfig(string tableId) =>
        Tables.FirstOrDefault(t => t.TableId == tableId)
        ?? throw new InvalidOperationException($"Unknown table {tableId}");

    public IReadOnlyList<TableConfig> GetAllTables() => Tables;
}