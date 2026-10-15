namespace ITServiceDeskApp.ViewModels;

public sealed class InventoryTablePage
{
    public int Total { get; }
    public int PageSize { get; }
    public int Page { get; }
    public int Pages { get; }
    public int Skip => (Page - 1) * PageSize;
    public int First => Total == 0 ? 0 : Skip + 1;
    public int Last => Math.Min(Skip + PageSize, Total);
    public InventoryTablePage(int total, int page, int pageSize)
    {
        Total = Math.Max(0, total);
        PageSize = pageSize is 25 or 50 or 100 ? pageSize : 25;
        Pages = Math.Max(1, (int)Math.Ceiling(Total / (double)PageSize));
        Page = Math.Clamp(page, 1, Pages);
    }
}
