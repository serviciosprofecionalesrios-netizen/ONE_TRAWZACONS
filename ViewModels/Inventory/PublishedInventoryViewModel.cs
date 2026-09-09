namespace ITServiceDeskApp.ViewModels.Inventory;

public record InventorySheet(string Key, string Name, string Gid, string[] IdentityColumns);
public record InventorySourceRow(int SourceRow, string[] Cells);
public record InventorySourceTable(string[] Headers, IReadOnlyList<InventorySourceRow> Rows, int ExcludedRows);
public record InventorySourceResult(InventorySourceTable? Table, DateTimeOffset? RetrievedAt, string? Warning);

public class PublishedInventoryViewModel
{
    public required InventorySheet Sheet { get; init; }
    public required InventorySourceResult Source { get; init; }
    public string Query { get; init; } = "";
    public int Page { get; init; }
    public int TotalRows { get; init; }
    public int TotalPages => Math.Max(1, (TotalRows + 49) / 50);
    public IReadOnlyList<InventorySourceRow> Rows { get; init; } = [];
}
