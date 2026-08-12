namespace CrashLab.GameEngine.Grains;

public interface ITableCatalog
{
    TableConfig GetConfig(string tableId);
    IReadOnlyList<TableConfig> GetAllTables();
}