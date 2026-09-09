namespace ITServiceDeskApp.ViewModels.Inventory;

public class PublishedRepairsViewModel : PublishedInventoryViewModel
{
    public string Status { get; init; } = "";
    public IReadOnlyDictionary<string, int> StatusCounts { get; init; } = new Dictionary<string, int>();
    public static string State(InventorySourceTable table, InventorySourceRow row)
    {
        var index = Array.FindIndex(table.Headers, h => h.Equals("Estado", StringComparison.OrdinalIgnoreCase));
        return index >= 0 && !string.IsNullOrWhiteSpace(row.Cells[index]) ? row.Cells[index].Trim() : "Sin estado";
    }
}
