namespace ITServiceDeskApp.ViewModels.Inventory
{
    public class InventoryHubViewModel
    {
        public int ItAssetsTotal { get; set; }
        public int ItAssetsActive { get; set; }
        public int SparePartsTotal { get; set; }
        public int SparePartsLowStock { get; set; }
        public int SparePartsOutOfStock { get; set; }
        public int PurchaseRequestsOpen { get; set; }
    }
}
