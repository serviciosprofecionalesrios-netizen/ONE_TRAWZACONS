namespace ITServiceDeskApp.ViewModels.Inventory
{
    public class InventoryHubViewModel
    {
        public IReadOnlyDictionary<string, InventorySourceResult> PublishedSources { get; set; } = new Dictionary<string, InventorySourceResult>();
        public int ItAssetsTotal { get; set; }
        public int ItAssetsActive { get; set; }
        public int SparePartsTotal { get; set; }
        public int SparePartsLowStock { get; set; }
        public int SparePartsOutOfStock { get; set; }
        public int PurchaseRequestsOpen { get; set; }
    }

    public class PurchaseDashboardViewModel
    {
        public DateTime? From { get; init; }
        public DateTime? To { get; init; }
        public DateTime? LatestPurchaseDate { get; init; }
        public int PurchaseOrders { get; init; }
        public int PurchaseLines { get; init; }
        public decimal TotalCordobas { get; init; }
        public decimal TotalUsd { get; init; }
        public IReadOnlyList<PurchaseDashboardStatus> ByStatus { get; init; } = [];
        public IReadOnlyList<PurchaseDashboardSupplier> TopSuppliers { get; init; } = [];
        public IReadOnlyList<PurchaseDashboardOrder> RecentOrders { get; init; } = [];
        public string? Warning { get; init; }
    }

    public record PurchaseDashboardStatus(string Name, int Orders, decimal Cordobas, decimal Usd);
    public record PurchaseDashboardSupplier(string Name, int Orders, decimal Cordobas, decimal Usd);
    public record PurchaseDashboardOrder(string OrderNumber, string Supplier, string Status, DateTime? Date, decimal Cordobas, decimal Usd, string Description);
}
