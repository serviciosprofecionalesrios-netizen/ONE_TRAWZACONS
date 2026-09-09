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
    public string Value(InventorySourceRow row, string column)
    {
        var index = Array.FindIndex(Source.Table?.Headers ?? [], h => h.Equals(column, StringComparison.OrdinalIgnoreCase));
        return index >= 0 && index < row.Cells.Length && !string.IsNullOrWhiteSpace(row.Cells[index]) ? row.Cells[index] : "—";
    }
    public string Description => Sheet.Key switch
    {
        "inventario" => "Existencias, ubicación y catálogo de artículos.",
        "entradas" => "Recepciones de materiales y seguimiento de entregas.",
        "salidas" => "Despachos, requisiciones y destino de los materiales.",
        _ => "Compras, proveedores y estado de las órdenes."
    };
    public string Icon => Sheet.Key switch { "inventario" => "bi-box-seam", "entradas" => "bi-box-arrow-in-down", "salidas" => "bi-box-arrow-up-right", _ => "bi-receipt" };
}
